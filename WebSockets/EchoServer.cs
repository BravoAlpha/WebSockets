using System;
using System.Net;

namespace WebSockets
{
    public class EchoServer
    {
        private readonly WebSocketServer m_server;

        public EchoServer(IPAddress address, int port)
        {
            m_server = new WebSocketServer(address, port);
            m_server.OnClientConnected += OnClientConnected;
        }

        public void Start()
        {
            m_server.Start();
        }

        public void Stop()
        {
            m_server.Stop();
        }

        private void OnClientConnected(ClientConnection client)
        {
            client.ReceivedTextualData += OnReceivedTextualData;
            client.Disconnected += OnClientDisconnected;
            client.StartReceiving();

            Console.WriteLine("Client {0} Connected...", client.Id);
        }

        private void OnClientDisconnected(ClientConnection client)
        {
            client.ReceivedTextualData -= OnReceivedTextualData;
            client.Disconnected -= OnClientDisconnected;

            Console.WriteLine("Client {0} Disconnected...", client.Id);
        }

        private void OnReceivedTextualData(ClientConnection client, string data)
        {
            Console.WriteLine("Client {0} Received Message: {1}", client.Id, data);
            client.Send(data);
        }
    }
}