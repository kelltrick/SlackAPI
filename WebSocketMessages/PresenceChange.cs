using System;

namespace SlackAPI.WebSocketMessages
{
    public class PresenceChange : SlackSocketMessage
    {
        [SlackSocketRouting("presence_change")]
        public PresenceChange()
        { }
        public string user;
        public Presence presence;
    }
}
