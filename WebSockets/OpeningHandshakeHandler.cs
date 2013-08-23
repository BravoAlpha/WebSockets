using System;
using System.Security.Cryptography;
using System.Text;

namespace WebSockets
{
    // Note: This implementation does not support sub-protocol negotioation.
    public static class OpeningHandshakeHandler
    {
        public static string CreateServerHandshake(string clientHandshake)
        {
            string clientKey = ExtractClientKeyFromHandshake(clientHandshake);
            if (String.IsNullOrEmpty(clientKey))
                return String.Empty;

            return EncodeServerHandshake(clientKey);
        }

        private static string ExtractClientKeyFromHandshake(string clientHandshake)
        {
            const string secWebSocketKeyHeader = "Sec-WebSocket-Key: ";

            int indexOfKeyHeader = clientHandshake.IndexOf(secWebSocketKeyHeader, StringComparison.InvariantCulture);
            if (indexOfKeyHeader == -1)
                return String.Empty;

            int keyStartPosition = indexOfKeyHeader + secWebSocketKeyHeader.Length;

            int endOfLineIndex = clientHandshake.IndexOf('\n', indexOfKeyHeader);
            if (endOfLineIndex == -1)
                return String.Empty;

            string key = clientHandshake.Substring(keyStartPosition, endOfLineIndex - keyStartPosition - 1);
            return key;
        }

        private static string EncodeServerHandshake(string clientKey)
        {
            const string keySuffix = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            const string handshake =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: {0}\r\n\r\n";

            var paddedKey = clientKey + keySuffix;
            var hasher = SHA1.Create();
            string encodedKey = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(paddedKey)));

            string answer = String.Format(handshake, encodedKey);
            return answer;
        }
    }
}