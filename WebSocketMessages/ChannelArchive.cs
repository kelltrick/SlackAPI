namespace SlackAPI.WebSocketMessages
{
    public class ChannelArchive
    {
        [SlackSocketRouting("channel_archive")]
        public ChannelArchive()
        { }
        public string channel;
        public string user;
    }
}
