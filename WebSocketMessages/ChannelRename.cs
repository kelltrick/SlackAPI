namespace SlackAPI.WebSocketMessages
{
    public class ChannelRename
    {
        [SlackSocketRouting("channel_rename")]
        public ChannelRename()
        { }
        public Channel channel;
    }
}
