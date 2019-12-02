using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class OffsetResponse : IMessage
    {
        [Key(0)]
        public long Offset { get; set; }
        [Key(1)]
        public string ConsumerGroup { get; set; }
        [Key(2)]
        public string Topic { get; set; }
        [Key(3)]
        public int Partition { get; set; }
    }
}
