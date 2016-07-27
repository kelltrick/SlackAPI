namespace SlackAPI.WebSocketMessages
{
    public class ChannelDeleted
    {
        [SlackSocketRouting("channel_deleted")]
        public ChannelDeleted()
        { }
        public string channel;
    }
}
