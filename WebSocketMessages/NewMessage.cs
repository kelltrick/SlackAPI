using System;

namespace SlackAPI.WebSocketMessages
{
    public class NewMessage : SlackSocketMessage
    {
        public string user;
        public string channel;
        public string text;
        public string team;
        public DateTime ts;

        [SlackSocketRouting("message")]
        [SlackSocketRouting("message", "bot_message")]
        public NewMessage()
        {
            type = "message";
        }
    }
}
