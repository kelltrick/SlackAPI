namespace SlackAPI.WebSocketMessages
{
    public class GroupRename : SlackSocketMessage
    {
        [SlackSocketRouting("group_rename")]
        public GroupRename()
        { }
        public Channel channel;
    }
}

