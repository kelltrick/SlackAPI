namespace SlackAPI.WebSocketMessages
{
    public class ChannelCreated
    {
        [SlackSocketRouting("channel_created")]
        public ChannelCreated()
        { }
        public Channel channel;
    }
}
