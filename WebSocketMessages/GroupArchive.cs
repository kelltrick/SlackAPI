namespace SlackAPI.WebSocketMessages
{
    public class GroupArchive : SlackSocketMessage
    {
        [SlackSocketRouting("group_archive")]
        public GroupArchive()
        { }
        public string channel;
    }
}

