using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class NoNewMessage : IMessage
    {
    }
}
