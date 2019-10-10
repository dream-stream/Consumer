using System.Linq;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using MessagePack;

namespace Consumer.Services
{
    public class MessageProcessor
    {
        public byte[] Serialize<T>(T message)
        {
            return LZ4MessagePackSerializer.Serialize(message);
        }

        public T Deserialize<T>(byte[] message)
        {
            return LZ4MessagePackSerializer.Deserialize<T>(message);
        }

        public async Task<T> ReceiveMessage<T>(SocketService socket) where T : IMessage
        {
            var buffer = new byte[1024 * 4];
            var result = await socket.ReceiveMessage(buffer);

            var message = Deserialize<T>(buffer.Take(result.Count).ToArray());

            return message;
        }
    }
}
