using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;

namespace Consumer.Services
{
    public interface IConsumer
    {
        Task Connect(string connectionString);
        Task Subscribe(string topic, Action<MessageContainer> messageHandler);
        Task UnSubscribe(Guid consumerId);
        Task CloseConnection();
    }
}
