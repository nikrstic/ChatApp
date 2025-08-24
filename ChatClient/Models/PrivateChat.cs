using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatClient.Models
{
    public class PrivateChat
    {

        public string User1 { get; set; }
        public string User2 { get; set; }
        public ObservableCollection<string> Messages { get; } =     new();

        public string ChatId => $"{User1}-{User2}"; 
    }
}
