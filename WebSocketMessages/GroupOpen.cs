namespace SlackAPI.WebSocketMessages
{
    public class GroupOpen : SlackSocketMessage
    {
        [SlackSocketRouting("group_open")]
        public GroupOpen()
        { }
        public string user;
        public string channel;
    }
}

