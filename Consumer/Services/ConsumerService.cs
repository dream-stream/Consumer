using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;

namespace Consumer.Services
{
    public class ConsumerService : IConsumer
    {
        private readonly Guid _consumerId;
        private ulong _offset;
        private readonly int _readSize;
        private readonly BrokerSocket _brokerSocket;
        private readonly MessageProcessor _messageProcessor;

        public ConsumerService(BrokerSocket brokerSocket, MessageProcessor messageProcessor)
        {
            _brokerSocket = brokerSocket ?? throw new ArgumentNullException(nameof(brokerSocket));
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _offset = 0;
            _readSize = 1024 * 4;
            _consumerId = Guid.NewGuid();
        }

        public async Task Connect(string connectionString)
        {
            await _brokerSocket.ConnectToBroker(connectionString);
        }

        public async Task Subscribe(string topic, string consumerGroup, Action<IMessage> messageHandler)
        {
            var subscriptionRequest = new SubscriptionRequest
            {
                Topic = topic,
                ConsumerGroup = consumerGroup,
                ConsumerId = _consumerId
            };

            await _brokerSocket.SendMessage(_messageProcessor.Serialize<IMessage>(subscriptionRequest));
            var subResponse = await _messageProcessor.ReceiveMessage<IMessage>(_brokerSocket, _readSize) as SubscriptionResponse;
            Console.WriteLine(subResponse?.TestMessage);

            //Poll loop
            while (true)
            {
#pragma warning disable 4014
                _brokerSocket.SendMessage(_messageProcessor.Serialize<IMessage>(new MessageRequest
                {
                    Topic = "MyTopic",
                    Partition = 3,
                    OffSet = _offset,
                    ReadSize = _readSize
                }));
#pragma warning restore 4014

                var receivedSize = await _messageProcessor.ReceiveMessage<IMessage>(_brokerSocket, _readSize, messageHandler);
                _offset += receivedSize;
                
            }
        }

        public async Task UnSubscribe(Guid consumerId)
        {
            await Task.Run(() => Task.CompletedTask); //TODO Unsubscribe
        }

        public async Task CloseConnection()
        {
            await _brokerSocket.CloseConnection();
        }
    }
}
