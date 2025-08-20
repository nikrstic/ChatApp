using ChatServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int port = 3234;
            Console.WriteLine($"Pokrećem server na portu {port}...");
            Server.Start(port);

        }
    }
}
