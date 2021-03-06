﻿using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using dotnet_etcd;

namespace Consumer.Services
{
    public interface IConsumer
    {
        Task<ConsumerGroupTable> Subscribe(string topic, string consumerGroup,
            Action<MessageRequestResponse> messageHandler);
        Task UnSubscribe(Guid consumerId);
        Task InitSockets(EtcdClient client);
    }
}
