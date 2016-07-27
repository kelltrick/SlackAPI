namespace SlackAPI.WebSocketMessages
{
    public class GroupClose : SlackSocketMessage
    {
        [SlackSocketRouting("group_close")]
        public GroupClose()
        { }
        public string user;
        public string channel;
    }
}

