using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatClient.Models
{
    public class EmojiModel
    {
       
        public class EmojiGroup
        {
            public string? GroupName { get; set; }
            public List<EmojiSubgroup> Subgroups { get; set; } = new();
        }

        public class EmojiSubgroup
        {
            public string? SubgroupName { get; set; }
            public List<string> Emojis { get; set; } = new();
        }
      
    }
}
