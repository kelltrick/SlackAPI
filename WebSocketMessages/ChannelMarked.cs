using System;

namespace SlackAPI.WebSocketMessages
{
    public class ChannelMarked : SlackSocketMessage
    {
        [SlackSocketRouting("channel_marked")]
        public ChannelMarked()
        { }
        public string channel;
        public DateTime ts;
    }
}
