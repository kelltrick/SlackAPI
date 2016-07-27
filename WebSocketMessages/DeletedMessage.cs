using System;

namespace SlackAPI.WebSocketMessages
{
    public class DeletedMessage : SlackSocketMessage
    {
        [SlackSocketRouting("message", "message_deleted")]
        public DeletedMessage()
        { }
        public string channel;
        public DateTime ts;
        public DateTime deleted_ts;
        public bool hidden;
    }
}
