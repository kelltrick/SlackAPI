using System.IO;
using Newtonsoft.Json;
using SlackAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SlackAPI.WebSocketMessages;

#if WINDOWS_UWP
using Windows.Networking.Sockets;
using Windows.System.Threading;
using Windows.Storage.Streams;
#else
using System.Net.WebSockets;
#endif

namespace SlackAPI
{
    public static class SocketExtensions
    {
#if WINDOWS_UWP
        public static WebSocketState State(this MessageWebSocket socket)
        {
            return WebSocketState.None;
        }
#else
        public static WebSocketState State(this ClientWebSocket socket)
        {
            return socket.State;
        }
#endif
    }

#if WINDOWS_UWP
    public enum WebSocketState
    {
        None = 0,
        Connecting = 1,
        Open = 2,
        CloseSent = 3,
        CloseReceived = 4,
        Closed = 5,
        Aborted = 6
    }
#endif

    public class SlackSocket
    {
        LockFreeQueue<string> sendingQueue;
        int currentlySending;
        int closedEmitted;

        Dictionary<int, Action<string>> callbacks;
#if WINDOWS_UWP
        internal WebSocketState socketState;
        internal MessageWebSocket socket;

        public event Action<Exception> ErrorSending;
        public event Action<Exception> ErrorReceiving;
#else
        CancellationTokenSource cts;
        internal ClientWebSocket socket;
        
        public event Action<WebSocketException> ErrorSending;
        public event Action<WebSocketException> ErrorReceiving;
#endif
        int currentId;

        Dictionary<string, Dictionary<string, Delegate>> routes;
        public bool Connected { get { return socket != null && socket.State() == WebSocketState.Open; } }
        public event Action<Exception> ErrorReceivingDesiralization;
        public event Action<Exception> ErrorHandlingMessage;
        public event Action ConnectionClosed;

        //This would be done for hinting but I don't think we really need this.

        static Dictionary<string, Dictionary<string, Type>> routing;
        static SlackSocket()
        {
            routing = new Dictionary<string, Dictionary<string, Type>>();

#if WINDOWS_UWP
            Assembly currentAssembly = typeof(SlackClient).GetTypeInfo().Assembly;
            foreach (Type t in typeof(SlackClient).GetTypeInfo().Assembly.GetTypes())
                foreach (SlackSocketRouting route in t.GetConstructor(new Type[0]).GetCustomAttributes<SlackSocketRouting>())
                {
                    if (!routing.ContainsKey(route.Type))
                        routing.Add(route.Type, new Dictionary<string, Type>()
                            {
                                {route.SubType ?? "null", t}
                            });
                    else
                        if (!routing[route.Type].ContainsKey(route.SubType ?? "null"))
                        routing[route.Type].Add(route.SubType ?? "null", t);
                    else
                        throw new InvalidProgramException("Cannot have two socket message types with the same type and subtype!");
                }

#else
            foreach (Assembly assy in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assy.GlobalAssemblyCache)
                    foreach (Type t in assy.GetTypes())
                        foreach (SlackSocketRouting route in t.GetConstructor(new Type[0]).GetCustomAttributes<SlackSocketRouting>())
                        {
                            if (!routing.ContainsKey(route.Type))
                                routing.Add(route.Type, new Dictionary<string, Type>()
                            {
                                {route.SubType ?? "null", t}
                            });
                            else
                                if (!routing[route.Type].ContainsKey(route.SubType ?? "null"))
                                    routing[route.Type].Add(route.SubType ?? "null", t);
                                else
                                    throw new InvalidProgramException("Cannot have two socket message types with the same type and subtype!");
                        }
            }
#endif
        }

        public SlackSocket(LoginResponse loginDetails, object routingTo, Action onConnected = null)
        {
            BuildRoutes(routingTo);
#if WINDOWS_UWP
            socket = new MessageWebSocket();
            socket.Control.MessageType = SocketMessageType.Utf8;
            socket.MessageReceived += MessageReceived;
            socket.Closed += Closed;
            socketState = WebSocketState.None;
#else
            socket = new ClientWebSocket();
#endif

            callbacks = new Dictionary<int, Action<string>>();
            sendingQueue = new LockFreeQueue<string>();
            currentId = 1;

            Uri slackApiUri = new Uri(string.Format("{0}?svn_rev={1}&login_with_boot_data-0-{2}&on_login-0-{2}&connect-1-{2}", loginDetails.url, loginDetails.svn_rev, DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds));
#if WINDOWS_UWP
            socketState = WebSocketState.Connecting;
            socket.ConnectAsync(slackApiUri).GetResults();
            socketState = WebSocketState.Open;
#else
            cts = new CancellationTokenSource();
            socket.ConnectAsync(slackApiUri, cts.Token).Wait();
            SetupReceiving();
#endif
            if (onConnected != null)
                onConnected();
        }

        void BuildRoutes(object routingTo)
        {
            routes = new Dictionary<string, Dictionary<string, Delegate>>();

            Type routingToType = routingTo.GetType();
            Type slackMessage = typeof(SlackSocketMessage);
            foreach (MethodInfo m in routingTo.GetType().GetMethods(BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic | BindingFlags.Public))
            {
                ParameterInfo[] parameters = m.GetParameters();
                if (parameters.Length != 1) continue;
                if (parameters[0].ParameterType.IsSubclassOf(slackMessage))
                {
                    Type t = parameters[0].ParameterType;
                    foreach (SlackSocketRouting route in t.GetCustomAttributes<SlackSocketRouting>())
                    {
                        Type genericAction = typeof(Action<>).MakeGenericType(parameters[0].ParameterType);
                        Delegate d = Delegate.CreateDelegate(genericAction, routingTo, m, false);
                        if (d == null)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Couldn't create delegate for {0}.{1}", routingToType.FullName, m.Name));
                            continue;
                        }
                        if (!routes.ContainsKey(route.Type))
                            routes.Add(route.Type, new Dictionary<string, Delegate>());
                        if (!routes[route.Type].ContainsKey(route.SubType ?? "null"))
                            routes[route.Type].Add(route.SubType ?? "null", d);
                        else
                            routes[route.Type][route.SubType ?? "null"] = Delegate.Combine(routes[route.Type][route.SubType ?? "null"], d);
                    }
                }
            }
        }

        public void Send<K>(SlackSocketMessage message, Action<K> callback)
            where K : SlackSocketMessage
        {
            int sendingId = Interlocked.Increment(ref currentId);
            message.id = sendingId;
            callbacks.Add(sendingId, (c) =>
            {
                K obj = c.Deserialize<K>();
                callback(obj);
            });
            Send(message);
        }

        public void Send(SlackSocketMessage message)
        {
            if (message.id == 0)
                message.id = Interlocked.Increment(ref currentId);
            //socket.Send(JsonConvert.SerializeObject(message));

            if (string.IsNullOrEmpty(message.type)){
                IEnumerable<SlackSocketRouting> routes = message.GetType().GetCustomAttributes<SlackSocketRouting>();

                SlackSocketRouting route = null;
                foreach (SlackSocketRouting r in routes)
                {
                    route = r;
                }
                if (route == null) throw new InvalidProgramException("Cannot send without a proper route!");
                else
                {
                    message.type = route.Type;
                    message.subtype = route.SubType;
                }
            }

            sendingQueue.Push(JsonConvert.SerializeObject(message, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            if (Interlocked.CompareExchange(ref currentlySending, 1, 0) == 0)
#if WINDOWS_UWP
                ThreadPool.RunAsync(HandleSending).GetResults();
#else
                ThreadPool.QueueUserWorkItem(HandleSending);
#endif
        }

        public void BindCallback<K>(Action<K> callback)
        {
            Type t = typeof(K);

            foreach (SlackSocketRouting route in t.GetCustomAttributes<SlackSocketRouting>())
            {
                if (!routes.ContainsKey(route.Type))
                    routes.Add(route.Type, new Dictionary<string, Delegate>());
                if (!routes[route.Type].ContainsKey(route.SubType ?? "null"))
                    routes[route.Type].Add(route.SubType ?? "null", callback);
                else
                    routes[route.Type][route.SubType ?? "null"] = Delegate.Combine(routes[route.Type][route.SubType ?? "null"], callback);
            }
        }

        public void UnbindCallback<K>(Action<K> callback)
        {
            Type t = typeof(K);
            foreach (SlackSocketRouting route in t.GetCustomAttributes<SlackSocketRouting>())
            {
                Delegate d = routes.ContainsKey(route.Type) ? (routes.ContainsKey(route.SubType ?? "null") ? routes[route.Type][route.SubType ?? "null"] : null) : null;
                if (d != null)
                {
                    Delegate newd = Delegate.Remove(d, callback);
                    routes[route.Type][route.SubType ?? "null"] = newd;
                }
            }
        }

#if WINDOWS_UWP
        //The MessageReceived event handler.
        private void MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            DataReader messageReader = args.GetDataReader();
            messageReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            string messageString = messageReader.ReadString(messageReader.UnconsumedBufferLength);

            //Add code here to do something with the string that is received.
        }

        private async Task SendMessage(MessageWebSocket webSock, string message)
        {
            DataWriter messageWriter = new DataWriter(webSock.OutputStream);
            messageWriter.WriteString(message);
            await messageWriter.StoreAsync();
        }

        private void Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            //Add code here to do something when the connection is closed locally or by the server
        }
#else
        void SetupReceiving()
        {
            Task.Factory.StartNew(
                async () =>
                {
                    List<byte[]> buffers = new List<byte[]>();
                    byte[] bytes = new byte[1024];
                    buffers.Add(bytes);
                    ArraySegment<byte> buffer = new ArraySegment<byte>(bytes);
                    while (socket.State() == WebSocketState.Open)
                    {
                        WebSocketReceiveResult result = null;
                        try
                        {
                            result = await socket.ReceiveAsync(buffer, cts.Token);
                        }
                        catch (WebSocketException wex)
                        {
                            if (ErrorReceiving != null)
                                ErrorReceiving(wex);
                            Close();
                            break;
                        }

                        if (!result.EndOfMessage && buffer.Count == buffer.Array.Length)
                        {
                            bytes = new byte[1024];
                            buffers.Add(bytes);
                            buffer = new ArraySegment<byte>(bytes);
                            continue;
                        }

                        string data = string.Join("", buffers.Select((c) => Encoding.UTF8.GetString(c).TrimEnd('\0')));
                        //Console.WriteLine("SlackSocket data = " + data);
                        SlackSocketMessage message = null;
                        try
                        {
                            message = data.Deserialize<SlackSocketMessage>();
                        }
                        catch (JsonException jsonExcep)
                        {
                            if (ErrorReceivingDesiralization != null)
                                ErrorReceivingDesiralization(jsonExcep);
                            continue;
                        }

                        if (message == null)
                            continue;
                        else
                        {
                            HandleMessage(message, data);
                            buffers = new List<byte[]>();
                            bytes = new byte[1024];
                            buffers.Add(bytes);
                            buffer = new ArraySegment<byte>(bytes);
                        }
                    }
                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
#endif

        void HandleMessage(SlackSocketMessage message, string data)
        {
            if (callbacks.ContainsKey(message.reply_to))
                callbacks[message.reply_to](data);
            else if (routes.ContainsKey(message.type) && routes[message.type].ContainsKey(message.subtype ?? "null"))
            {
                try
                {
                    object o = null;
                    if (routing.ContainsKey(message.type) &&
                        routing[message.type].ContainsKey(message.subtype ?? "null"))
                        o = data.Deserialize(routing[message.type][message.subtype ?? "null"]);
                    else
                    {
                        //I believe this method is slower than the former. If I'm wrong we can just use this instead. :D
                        Type t = routes[message.type][message.subtype ?? "null"].Method.GetParameters()[0].ParameterType;
                        o = data.Deserialize(t);
                    }
                    routes[message.type][message.subtype ?? "null"].DynamicInvoke(o);
                }
                catch (Exception e)
                {
                    if (ErrorHandlingMessage != null)
                        ErrorHandlingMessage(e);
                    throw e;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(string.Format("No valid route for {0} - {1}", message.type, message.subtype ?? "null"));
                if (ErrorHandlingMessage != null)
                    ErrorHandlingMessage(new InvalidDataException(string.Format("No valid route for {0} - {1}", message.type, message.subtype ?? "null")));
            }
        }

        void HandleSending(object stateful)
        {
            string message;
            while (sendingQueue.Pop(out message) && socket.State() == WebSocketState.Open
#if !WINDOWS_UWP
                && !cts.Token.IsCancellationRequested
#endif
                )
            {
                byte[] sending = Encoding.UTF8.GetBytes(message);
                ArraySegment<byte> buffer = new ArraySegment<byte>(sending);
                try
                {
#if WINDOWS_UWP
#else
                    socket.SendAsync(buffer, WebSocketMessageType.Text, true, cts.Token).Wait();
#endif
                }
#if !WINDOWS_UWP
                catch (WebSocketException wex)
                {
                    if (ErrorSending != null)
                        ErrorSending(wex);
                    Close();
                    break;
                }
#else
                catch (Exception ex)
                {
                    if (ErrorSending != null)
                        ErrorSending(ex);
                    Close();
                    break;
                }
#endif
            }

            currentlySending = 0;
        }

        public void Close()
        {
            try
            {
#if WINDOWS_UWP
                this.socket.Close(204, string.Empty);
#else
                this.socket.close();
#endif
            }
            catch (Exception ex)
            {

            }

            if (Interlocked.CompareExchange(ref closedEmitted, 1, 0) == 0 && ConnectionClosed != null)
                ConnectionClosed();
        }


#if WINDOWS_UWP
        public WebSocketState State()
        {
            return socketState;
        }
#else
        public WebSocketState State()
        {
            return socket.State;
        }
#endif
    }

    public class SlackSocketMessage
    {
        public int id;
        public int reply_to;
        public string type;
        public string subtype;
        public bool ok = true;
        public Error error;
        public static readonly Type[] ChildClasses = new Type[]
        {
            typeof(ChannelMarked),
            typeof(DeletedMessage),
            typeof(GroupArchive),
            typeof(GroupClose),
            typeof(GroupJoined),
            typeof(ChannelMarked),
            typeof(ChannelMarked),
        };
    }

    public class Error
    {
        public int code;
        public string msg;
    }
    
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = true, Inherited = false)]
    public class SlackSocketRouting : Attribute
    {
        public string Type;
        public string SubType;
        public SlackSocketRouting(string type, string subtype = null)
        {
            this.Type = type;
            this.SubType = subtype;
        }
    }
}