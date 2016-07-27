namespace SlackAPI.WebSocketMessages
{
    public class ChannelUnarchive
    {
        [SlackSocketRouting("channel_unarchive")]
        public ChannelUnarchive()
        { }
        public string channel;
        public string user;
    }
}
