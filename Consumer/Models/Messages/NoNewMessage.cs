using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class NoNewMessage : IMessage
    {
        [Key(0)]
        public MessageHeader Header { get; set; }
    }
}
