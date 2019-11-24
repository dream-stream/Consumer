using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using dotnet_etcd;

namespace Consumer.Services
{
    public interface IConsumer
    {
        Task Subscribe(string topic, string consumerGroup, Action<MessageRequestResponse> messageHandler);
        Task UnSubscribe(Guid consumerId);
        Task CloseConnection();
        Task InitSockets(EtcdClient client);
    }
}
