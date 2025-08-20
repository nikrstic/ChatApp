using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatShared.Models
{
    public class Message
    {
        public string Type { get; set; }
        public string From { get; set; }

        public string Text { get; set; }
    }
}
