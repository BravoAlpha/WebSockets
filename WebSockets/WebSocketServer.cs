using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WebSockets
{
    public class WebSocketServer
    {
        private bool m_isStarted;
        private readonly TcpListener m_tcpListener;
        private readonly ConcurrentDictionary<Guid, ClientConnection> m_clients = new ConcurrentDictionary<Guid, ClientConnection>(); 
        private readonly object m_sync = new object();

        public event Action<ClientConnection> OnClientConnected = delegate { };

        public WebSocketServer(IPAddress address, int port)
        {
            m_tcpListener = new TcpListener(address, port);
        }

        public void Start()
        {
            if (m_isStarted)
                return;

            lock (m_sync)
            {
                if (m_isStarted)
                    return;

                m_isStarted = true;
                m_tcpListener.Start();
                m_tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
            }
        }

        public void Stop()
        {
            if (!m_isStarted)
                return;

            lock(m_sync)
            {
                if (!m_isStarted)
                    return;

                m_isStarted = false;
                m_tcpListener.Stop();

                foreach (var client in m_clients.Values)
                {
                    client.Disconnect();
                }
            }
        }

        private void OnAcceptClient(IAsyncResult asyncResult)
        {
            if (!m_isStarted)
                return;

            TcpClient client = m_tcpListener.EndAcceptTcpClient(asyncResult);
            ReceiveClientHandshake(client);

            m_tcpListener.BeginAcceptTcpClient(OnAcceptClient, null);
        }

        private void ReceiveClientHandshake(TcpClient client)
        {
            var buffer = new byte[1024];
            var socketAsyncEventArgs = new SocketAsyncEventArgs();

            socketAsyncEventArgs.UserToken = client;
            socketAsyncEventArgs.Completed += OnHandshakeReceived;
            socketAsyncEventArgs.SetBuffer(buffer, 0, buffer.Length);

            bool isAsync = client.Client.ReceiveAsync(socketAsyncEventArgs);
            if (!isAsync)
                OnHandshakeReceived(client.Client, socketAsyncEventArgs);
        }

        private void OnHandshakeReceived(object sender, SocketAsyncEventArgs e)
        {
            var client = (TcpClient) e.UserToken;

            int numberOfBytesReceived = e.SocketError != SocketError.Success ? 0 : e.BytesTransferred;
            if (numberOfBytesReceived <= 0)
            {
                client.Client.Shutdown(SocketShutdown.Both);
                client.Close();
                return;
            }

            // Note: We're working under the assumption that the entire handshake will arrive in one frame
            string data = Encoding.UTF8.GetString(e.Buffer, 0, e.BytesTransferred);
            string handshakeString = OpeningHandshakeHandler.CreateServerHandshake(data);
            if (String.IsNullOrEmpty(handshakeString))
            {
                client.Client.Shutdown(SocketShutdown.Both);
                client.Close();
                return;
            }

            byte[] handshakeBytes = Encoding.UTF8.GetBytes(handshakeString);
            SendHandshake(client, handshakeBytes);
        }

        private void SendHandshake(TcpClient client, byte[] handshake)
        {
            var sendEventArgs = new SocketAsyncEventArgs();

            sendEventArgs.UserToken = client;
            sendEventArgs.SetBuffer(handshake, 0, handshake.Length);
            sendEventArgs.Completed += OnHandshakeSendCompleted;

            client.Client.SendAsync(sendEventArgs);
        }

        private void OnHandshakeSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            var client = (TcpClient)e.UserToken;

            var clientConnection = new ClientConnection(Guid.NewGuid(), client);
            clientConnection.Disconnected += OnClientDisconnected;

            m_clients.TryAdd(clientConnection.Id, clientConnection);
            OnClientConnected(clientConnection);
        }

        private void OnClientDisconnected(ClientConnection client)
        {
            client.Disconnected -= OnClientDisconnected;

            ClientConnection clientConnection;
            m_clients.TryRemove(client.Id, out clientConnection);
        }
    }
}