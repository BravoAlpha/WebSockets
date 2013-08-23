using System;
using System.Net.Sockets;
using System.Text;

namespace WebSockets
{
    public class ClientConnection
    {
        private readonly TcpClient m_tcpClient;
        private bool m_isConnected;

        public Guid Id { get; private set; }

        public event Action<ClientConnection, string> ReceivedTextualData = delegate { };
        public event Action<ClientConnection, byte[]> ReceivedBinaryData = delegate { };
        public event Action<ClientConnection> Disconnected = delegate { };

        public ClientConnection(Guid id, TcpClient tcpClient)
        {
            m_tcpClient = tcpClient;
            Id = id;
            m_isConnected = true;
        }

        public void StartReceiving()
        {
            var buffer = new byte[1024];
            var socketAsyncEventArgs = new SocketAsyncEventArgs();

            socketAsyncEventArgs.Completed += OnDataReceived;
            socketAsyncEventArgs.SetBuffer(buffer, 0, buffer.Length);

            bool isAsync = m_tcpClient.Client.ReceiveAsync(socketAsyncEventArgs);
            if (!isAsync)
                OnDataReceived(m_tcpClient, socketAsyncEventArgs);
        }

        private void OnDataReceived(object sender, SocketAsyncEventArgs e)
        {
            if (!m_isConnected)
                return;

            int numberOfBytesReceived = e.SocketError != SocketError.Success ? 0 : e.BytesTransferred;
            if (numberOfBytesReceived <= 0)
            {
                Disconnect();
                return;
            }

            if (HandleFrame(e))
                StartReceiving();
        }

        private bool HandleFrame(SocketAsyncEventArgs args)
        {
            Frame frame = Frame.FromBuffer(args.Buffer);

            if (frame.Opcode == Frame.Opcodes.Close)
            {
                Disconnect();
                return false;
            }

            // Note: No support for fragmented messages
            if (frame.Opcode == Frame.Opcodes.Binary)
                ReceivedBinaryData(this, frame.UnmaskedPayload);
            else if (frame.Opcode == Frame.Opcodes.Text)
            {
                string textContent = Encoding.UTF8.GetString(frame.UnmaskedPayload, 0, (int)frame.PayloadLength);
                ReceivedTextualData(this, textContent);
            }

            return true;
        }

        public void Send(byte[] data)
        {
            var frame = new Frame(Frame.Opcodes.Binary, data, true);
            Send(frame);
        }

        public void Send(string data)
        {
            var frame = new Frame(Frame.Opcodes.Text, Encoding.UTF8.GetBytes(data), true);
            Send(frame);
        }

        private void Send(Frame frame)
        {
            if (!m_isConnected)
                return;

            byte[] buffer = frame.ToBuffer();

            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(buffer, 0, buffer.Length);

            m_tcpClient.Client.SendAsync(sendEventArgs);
        }

        public void Disconnect()
        {
            Frame closingFrame = Frame.CreateClosingFrame();
            Send(closingFrame);

            m_isConnected = false;

            m_tcpClient.Client.Shutdown(SocketShutdown.Both);
            m_tcpClient.Close();

            Disconnected(this);
        }
    }
}