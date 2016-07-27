using System;

namespace SlackAPI.WebSocketMessages
{
    public class Hello : SlackSocketMessage
    {
        [SlackSocketRouting("hello")]
        public Hello()
        { }
    }
}
