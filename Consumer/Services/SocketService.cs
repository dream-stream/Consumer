using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Consumer.Services
{
    public class SocketService
    {
        private readonly ClientWebSocket _clientWebSocket;
        private readonly Semaphore _semaphore;
        private readonly Semaphore _semaphore2;

        public SocketService()
        {
            _clientWebSocket = new ClientWebSocket();
            _semaphore = new Semaphore(1, 1);
            _semaphore2 = new Semaphore(1, 1);
        }

        public async Task ConnectToBroker(string connectionString)
        {
            await _clientWebSocket.ConnectAsync(new Uri(connectionString), CancellationToken.None);
        }

        public async Task SendMessage(byte[] message)
        {
            _semaphore.WaitOne();
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(message, 0, message.Length), WebSocketMessageType.Binary, false, CancellationToken.None);
            _semaphore.Release();
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
