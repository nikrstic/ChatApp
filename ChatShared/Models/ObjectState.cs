using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatShared.Models
{
    public class ObjectState
    {
        public const int bufferSize = 256;
        public Socket wSocket = null;
        public byte[] buffer = new byte[bufferSize];
        public StringBuilder sb = new StringBuilder();





    }
}
