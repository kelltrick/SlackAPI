using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlackAPI.WebSocketMessages
{
    public class ReactionAdded : SlackSocketMessage
    {
        [SlackSocketRouting("reaction_added")]
        public ReactionAdded()
        { }
        public string user;
        public string reaction;
        public string item_user;
        public Item item;
        public string event_ts;

        public ReactionAdded(){}
    }

    public class Item
    {
        public string type;
        public string channel;
        public string file;
        public string file_comment;
        public string ts;
    }
}

