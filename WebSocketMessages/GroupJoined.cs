namespace SlackAPI.WebSocketMessages
{
    public class GroupJoined : SlackSocketMessage
    {
        [SlackSocketRouting("group_joined")]
        public GroupJoined()
        { }
        public Channel channel;
    }
}

