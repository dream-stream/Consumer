using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class OffsetResponse : IMessage
    {
        [Key(0)]
        public long Offset { get; set; }
    }
}
