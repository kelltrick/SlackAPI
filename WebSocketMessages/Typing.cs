using System;

namespace SlackAPI.WebSocketMessages
{
    public class Typing : SlackSocketMessage
    {
        [SlackSocketRouting("typing")]
        [SlackSocketRouting("user_typing")]
        public Typing()
        { }
        public string user;
        public string channel;
    }
}
