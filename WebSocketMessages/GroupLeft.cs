namespace SlackAPI.WebSocketMessages
{
    public class GroupLeft : SlackSocketMessage
    {
        [SlackSocketRouting("group_left")]
        public GroupLeft()
        { }
        public Channel channel;
    }
}

