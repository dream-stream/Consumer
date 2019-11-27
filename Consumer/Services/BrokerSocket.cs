using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Consumer.Services
{
    public class BrokerSocket
    {
        private readonly ClientWebSocket _clientWebSocket;
        private readonly Semaphore _lock;
        private readonly Semaphore _lock2;
        public string ConnectedTo { get; set; }


        public BrokerSocket()
        {
            _clientWebSocket = new ClientWebSocket();
            _lock = new Semaphore(1, 1);
            _lock2 = new Semaphore(1, 1);
        }

        public async Task ConnectToBroker(string connectionString)
        {
            Console.WriteLine($"Connecting to broker {connectionString}");
            await _clientWebSocket.ConnectAsync(new Uri(connectionString), CancellationToken.None);
            ConnectedTo = connectionString;
        }

        public async Task SendMessage(byte[] message)
        {
            _lock.WaitOne();
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(message, 0, message.Length), WebSocketMessageType.Binary, false, CancellationToken.None);
            _lock.Release();
        }

        public async Task<WebSocketReceiveResult> ReceiveMessage(byte[] buffer)
        {
            _lock2.WaitOne();
            var webSocketReceiveResult = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            _lock2.Release();
            return webSocketReceiveResult;
        }

        public async Task CloseConnection()
        {
            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", CancellationToken.None);
            Console.WriteLine("Closed the connection");
            _clientWebSocket.Abort();
            Console.WriteLine("Aborted the connection");
            _clientWebSocket.Dispose();
            Console.WriteLine("Disposed the clientWebSocket");
        }
        public bool IsOpen()
        {
            return _clientWebSocket.State == WebSocketState.Open;
        }
    }
}
