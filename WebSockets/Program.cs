using System;
using System.Net;

namespace WebSockets
{
    class Program
    {
        static void Main()
        {
            var server = new EchoServer(IPAddress.Loopback, 54321);
            server.Start();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            server.Stop();
        }
    }
}
