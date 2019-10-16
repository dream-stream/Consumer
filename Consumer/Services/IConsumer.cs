using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;

namespace Consumer.Services
{
    public interface IConsumer
    {
        Task Connect(string connectionString);
        Task Subscribe(string topic, string consumerGroup, Action<IMessage> messageHandler);
        Task UnSubscribe(Guid consumerId);
        Task CloseConnection();
    }
}
