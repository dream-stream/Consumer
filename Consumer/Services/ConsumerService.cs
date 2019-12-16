using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using dotnet_etcd;
using MessagePack;
using Mvccpb;

namespace Consumer.Services
{
    public class ConsumerService : IConsumer
    {
        private readonly Guid _consumerId;
        private string _consumerGroup;
        private readonly int _readSize;
        private HttpClient[] _brokerClients;
        private readonly Dictionary<string, HttpClient> _brokerClientDict = new Dictionary<string, HttpClient>();
        private EtcdClient _client;
        private readonly Semaphore _brokerClientHandlerLock = new Semaphore(1, 1);
        private string _topic;

        private CancellationTokenSource[] _cTokensForConsumerThreads;
        private Action<MessageRequestResponse> _messageHandler;

        public ConsumerService()
        {
            _readSize = 1024 * 900;
            _consumerId = Guid.NewGuid();

            Console.WriteLine($"Id - {_consumerId}");
        }

        public async Task InitSockets(EtcdClient client)
        {
            _client = client;
            _brokerClients = await BrokerHandler.UpdateBrokers(client, _brokerClients);
            await BrokerHandler.UpdateBrokerHttpClientsDictionary(client, _brokerClientDict, _brokerClients);
            client.WatchRange(BrokerHandler.BrokerTablePrefix, events =>
            {
                _brokerClientHandlerLock.WaitOne();
                _brokerClients = BrokerHandler.BrokerTableChangedHandler(events, _brokerClients);
                _brokerClientHandlerLock.Release();
            });
            client.WatchRange(BrokerHandler.TopicTablePrefix, events =>
            {
                _brokerClientHandlerLock.WaitOne();
                BrokerHandler.TopicTableChangedHandler(events, _brokerClientDict, _brokerClients);
                _brokerClientHandlerLock.Release();
            });
        }

        private async Task DoPolling(int partition, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Started Polling of partition {partition}");
            var offset = -1;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_brokerClientDict.TryGetValue($"{_topic}/{partition}", out var brokerClient))
                    {
                        var timeoutToken = new CancellationTokenSource(3000);
                        var response = await brokerClient.GetAsync($"api/broker?consumerGroup={_consumerGroup}&topic={_topic}&partition={partition}&offset={offset}&amount={_readSize}", timeoutToken.Token);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Non successful response from storage: {response.StatusCode} - partition {partition}");
                            continue;
                        }
                    
                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            Console.WriteLine($"No Content received - partition: {partition}");
                            await Task.Delay(500, cancellationToken);
                            if (offset == -1)
                                offset = 0;
                            continue;
                        }

                        var serializedData = await response.Content.ReadAsByteArrayAsync();
                        if (LZ4MessagePackSerializer.Deserialize<IMessage>(serializedData) is MessageRequestResponse data)
                        {
                            if (response.StatusCode == HttpStatusCode.PartialContent)
                            {
                                Console.WriteLine($"Moved Offset!!! with {data.Offset} - partition {partition}");
                                offset += data.Offset;
                                continue;
                            }

                            if (offset == -1)
                                offset = 0;

                            offset += data.Offset;
                            Console.WriteLine($"partition: {partition}, offset: {offset}");
                            _messageHandler(data);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to get brokerSocket {_topic}/{partition}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception thrown in DoPolling for partition {partition}");
                    Console.WriteLine(e);
                }
            }

            Console.WriteLine($"stopped pulling partition {partition}");
        }

        public async Task<ConsumerGroupTable> Subscribe(string topic, string consumerGroup,
            Action<MessageRequestResponse> messageHandler)
        {
            _messageHandler = messageHandler;
            _topic = topic;
            _consumerGroup = consumerGroup;
            var partitionCount = await TopicList.GetPartitionCount(_client, topic);
            _cTokensForConsumerThreads = new CancellationTokenSource[partitionCount];

            var consumerGroupTable = new ConsumerGroupTable(_client);
            await consumerGroupTable.ImHere(topic, _consumerGroup, _consumerId, PartitionsChangedHandler);

            return consumerGroupTable;
        }

        private void PartitionsChangedHandler(WatchEvent[] watchEvents)
        {
            foreach (var watchEvent in watchEvents)
            {
                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                        int[] partitions = null;
                        if (!string.IsNullOrEmpty(watchEvent.Value))
                            partitions = watchEvent.Value.Split(',').Select(int.Parse).ToArray();

                        var partitionIndex = 0;
                        for (var i = 0; i < _cTokensForConsumerThreads.Length; i++)
                        {
                            if (partitions != null && i == partitions[partitionIndex])
                            {
                                if(partitionIndex < partitions.Length - 1) partitionIndex++;
                                if (_cTokensForConsumerThreads[i] != null) continue;
                                // create new task and start and add cancellationToken to array
                                _cTokensForConsumerThreads[i] = new CancellationTokenSource();
                                var partition = i;
                                Task.Run(async () => { await DoPolling(partition, _cTokensForConsumerThreads[partition].Token); }, _cTokensForConsumerThreads[i].Token);
                            }
                            else if (_cTokensForConsumerThreads[i] != null)
                            {
                                Console.WriteLine($"Killing task {i}");
                                _cTokensForConsumerThreads[i].Cancel();
                                _cTokensForConsumerThreads[i].Dispose();
                                _cTokensForConsumerThreads[i] = null;
                            }
                        }

                        break;
                    case Event.Types.EventType.Delete:
                        //Todo maybe I don''t need to handle this one :s
                        throw new Exception("Lease expired - This should not have been deleted!!!!");
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public async Task UnSubscribe(Guid consumerId)
        {
            await Task.Run(() => Task.CompletedTask); //TODO Unsubscribe
        }
    }
}
