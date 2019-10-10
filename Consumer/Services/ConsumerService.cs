using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;

namespace Consumer.Services
{
    public class ConsumerService : IConsumer
    {
        private readonly Guid _consumerId;
        private int _offset;
        private int _readSize;
        private readonly SocketService _socketService;
        private readonly MessageProcessor _messageProcessor;

        public ConsumerService(SocketService socketService, MessageProcessor messageProcessor)
        {
            _socketService = socketService ?? throw new ArgumentNullException(nameof(socketService));
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _offset = 0;
            _readSize = 0;
            _consumerId = Guid.NewGuid();
        }

        public async Task Connect(string connectionString)
        {
            await _socketService.ConnectToBroker(connectionString);
        }

        public async Task Subscribe(string topic, Action<MessageContainer> messageHandler)
        {
            var subscriptionRequest = new SubscriptionRequest
            {
                Topic = topic
            };

            await _socketService.SendMessage(_messageProcessor.Serialize<IMessage>(subscriptionRequest));
            var subResponse = await _messageProcessor.ReceiveMessage<IMessage>(_socketService) as SubscriptionResponse;
            Console.WriteLine(subResponse?.TestMessage);

            do
            {
                await _socketService.SendMessage(_messageProcessor.Serialize<IMessage>(new MessageRequest()));
                var message = await _messageProcessor.ReceiveMessage<IMessage>(_socketService) as MessageContainer;
                messageHandler(message);
            } while (true);
        }

        public async Task UnSubscribe(Guid consumerId)
        {
            throw new NotImplementedException();
        }

        public async Task CloseConnection()
        {
            await _socketService.CloseConnection();
        }

        private void HandleSubscriptionMessage()
        {

        }
    }
}
