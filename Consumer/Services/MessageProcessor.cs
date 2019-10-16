using System;
using System.Linq;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using MessagePack;

namespace Consumer.Services
{
    public class MessageProcessor
    {
        public byte[] Serialize<T>(T message) where T : IMessage
        {
            return LZ4MessagePackSerializer.Serialize<IMessage>(message);
        }

        public T Deserialize<T>(byte[] message)
        {
            return LZ4MessagePackSerializer.Deserialize<T>(message);
        }

        public async Task<IMessage> ReceiveMessage<T>(BrokerSocket brokerSocket, int readSize) where T : IMessage
        {
            var buffer = new byte[readSize];
            var result = await brokerSocket.ReceiveMessage(buffer);

            var message = Deserialize<T>(buffer.Take(result.Count).ToArray());

            return message;
        }
        
        public async Task<ulong> ReceiveMessage<T>(BrokerSocket brokerSocket, int readSize, Action<IMessage> handler) where T : IMessage
        {
            var buffer = new byte[readSize];
            var result = await brokerSocket.ReceiveMessage(buffer);

#pragma warning disable 4014
            Task.Run(() =>
            {
                var message = Deserialize<T>(buffer.Take(result.Count).ToArray());
                handler(message);
            });
#pragma warning restore 4014

            return result.Count == 3 ? 0 : Convert.ToUInt64(result.Count);
        }
    }
}
