using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer.Models
{
    public class ClientInfo
    {
        public Socket Socket { get; set; }
        public int ID { get; set; }
        public string Nickname { get; set; }
    }
}
