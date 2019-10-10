using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class SubscriptionRequest : IMessage
    {
        [Key(1)]
        public string Topic { get; set; }
    }
}
