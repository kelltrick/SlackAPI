using System;

namespace SlackAPI.WebSocketMessages
{
    public class Ping : SlackSocketMessage
    {
        [SlackSocketRouting("ping")]
        public Ping()
        { }
        public int ping_interv_ms = 3000;
    }
}
