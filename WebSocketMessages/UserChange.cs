namespace SlackAPI.WebSocketMessages
{
    public class UserChange : SlackSocketMessage
    {
        [SlackSocketRouting("user_change")]
        public UserChange()
        { }
        public User user;
    }
}

