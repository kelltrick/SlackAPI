namespace SlackAPI.WebSocketMessages
{
    public class TeamJoin : SlackSocketMessage
    {
        [SlackSocketRouting("team_join")]
        public TeamJoin()
        { }
        public User user;
    }
}
