using System;

namespace SlackAPI.WebSocketMessages
{
    public class Pong : SlackSocketMessage
    {
        [SlackSocketRouting("pong")]
        public Pong()
        { }
        public int ping_interv_ms;
    }
}
