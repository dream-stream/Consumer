using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Consumer.Services
{
    public class BrokerSocket
    {
        private readonly ClientWebSocket _clientWebSocket;

        public BrokerSocket()
        {
            _clientWebSocket = new ClientWebSocket();
        }

        public async Task ConnectToBroker(string connectionString)
        {
            await _clientWebSocket.ConnectAsync(new Uri(connectionString), CancellationToken.None);
        }

        public async Task SendMessage(byte[] message)
        {
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(message, 0, message.Length), WebSocketMessageType.Binary, false, CancellationToken.None);
        }

        public async Task<WebSocketReceiveResult> ReceiveMessage(byte[] buffer)
        {
            return await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        public async Task CloseConnection()
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
        }
    }
}
