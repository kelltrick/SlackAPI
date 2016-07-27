namespace SlackAPI.WebSocketMessages
{
    public class GroupUnarchive : SlackSocketMessage
    {
        [SlackSocketRouting("group_unarchive")]
        public GroupUnarchive()
        { }
        public string channel;
    }
}

