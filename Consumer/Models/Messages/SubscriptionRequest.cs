using System;
using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class SubscriptionRequest : IMessage
    {
        [Key(1)]
        public string Topic { get; set; }
        [Key(2)]
        public string ConsumerGroup { get; set; }
        [Key(3)]
        public Guid ConsumerId { get; set; }
    }
}
