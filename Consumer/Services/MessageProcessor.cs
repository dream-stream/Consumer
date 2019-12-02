using System;
using System.Linq;
using System.Threading;
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

        private ulong testCounter = 0;

        public async Task<(MessageHeader header, long offset)> ReceiveMessage<T>(BrokerSocket brokerSocket, int readSize, Action<MessageRequestResponse> handler) where T : IMessage
        {
            var buffer = new byte[readSize];
            var result = await brokerSocket.ReceiveMessage(buffer);
            var message = Deserialize<T>(buffer.Take(result.Count).ToArray());

            switch (message)
            {
                case MessageRequestResponse msg:
#pragma warning disable 4014
                    Task.Run(() => handler(msg));
#pragma warning restore 4014

                    return (msg.Header, msg.Offset);
                case NoNewMessage msg:
                    if(testCounter++ % 1000 == 0)
                        Console.WriteLine("No new message * 1000");
                    return (msg.Header, 0);
                case OffsetResponse msg:
                    return (new MessageHeader {Topic = msg.Topic, Partition = msg.Partition}, msg.Offset + 1);
                default:
                    throw new Exception("Unknown message type");
            }
        }
    }
}
