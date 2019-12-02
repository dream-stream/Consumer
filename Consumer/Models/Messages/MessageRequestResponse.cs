using System.Collections.Generic;
using MessagePack;

namespace Consumer.Models.Messages
{
    [MessagePackObject]
    public class MessageRequestResponse
    {
        [Key(1)]
        public int Offset { get; set; }
        [Key(2)]
        public List<byte[]> Messages { get; set; }
        [Key(3)]
        public MessageHeader Header { get; set; }
    }
}
