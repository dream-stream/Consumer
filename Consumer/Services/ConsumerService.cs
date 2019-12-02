using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using dotnet_etcd;
using Mvccpb;

namespace Consumer.Services
{
    public class ConsumerService : IConsumer
    {
        private readonly Guid _consumerId;
        private string _consumerGroup;
        private static (SemaphoreSlim _lock, long _offset)[] _offsetHandler;
        private readonly int _readSize;
        private BrokerSocket[] _brokerSockets;
        private readonly ConcurrentDictionary<string, BrokerSocket> _brokerSocketsDict = new ConcurrentDictionary<string, BrokerSocket>();
        private readonly MessageProcessor _messageProcessor;
        private EtcdClient _client;
        private readonly Semaphore _brokerSocketHandlerLock = new Semaphore(1, 1);
        private readonly SemaphoreSlim _repartitionLock = new SemaphoreSlim(1, 1);
        private string _topic;

        private CancellationTokenSource[] _cTokensForConsumerThreads;
        private Action<MessageRequestResponse> _messageHandler;

        public ConsumerService(MessageProcessor messageProcessor)
        {
            _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            _readSize = 6000;
            _consumerId = Guid.NewGuid();

            Console.WriteLine($"Id - {_consumerId}");
        }

        public async Task InitSockets(EtcdClient client)
        {
            _client = client;
            _brokerSockets = await BrokerSocketHandler.UpdateBrokerSockets(client, _brokerSockets);
            await BrokerSocketHandler.UpdateBrokerSocketsDictionary(client, _brokerSocketsDict, _brokerSockets);
            client.WatchRange(BrokerSocketHandler.BrokerTablePrefix, async events =>
            {
                _brokerSocketHandlerLock.WaitOne();
                _brokerSockets = await BrokerSocketHandler.BrokerTableChangedHandler(events, _brokerSockets);
                _brokerSocketHandlerLock.Release();
            });
            client.WatchRange(BrokerSocketHandler.TopicTablePrefix, events =>
            {
                _brokerSocketHandlerLock.WaitOne();
                BrokerSocketHandler.TopicTableChangedHandler(events, _brokerSocketsDict, _brokerSockets);
                _brokerSocketHandlerLock.Release();
            });
        }

        private async Task DoPolling(int partition, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"Started Polling of partition {partition}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_repartitionLock.CurrentCount == 0)
                    {
                        await _repartitionLock.WaitAsync(cancellationToken);
                        _repartitionLock.Release();
                    } 
                    //await _offsetHandler[partition]._lock.WaitAsync(cancellationToken);
                    var offset = _offsetHandler[partition]._offset;

                    if (_brokerSocketsDict.TryGetValue($"{_topic}/{partition}", out var brokerSocket))
                    {
#pragma warning disable 4014
                        brokerSocket.SendMessage(_messageProcessor.Serialize<IMessage>(new MessageRequest
                        {
                            Topic = _topic,
                            Partition = partition,
                            OffSet = offset,
                            ReadSize = _readSize,
                            ConsumerGroup = _consumerGroup
                        }));
#pragma warning restore 4014

                        var (header, receivedSize) = await _messageProcessor.ReceiveMessage<IMessage>(brokerSocket, _readSize, _messageHandler);


                        if (_offsetHandler[header.Partition]._offset == -1)
                            _offsetHandler[header.Partition]._offset = 0;
                        _offsetHandler[header.Partition]._offset += receivedSize;
                        //_offsetHandler[header.Partition]._lock.Release();

                        if (receivedSize == 0)
                            await Task.Delay(500, cancellationToken);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to get brokerSocket {_topic}/{partition}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task Subscribe(string topic, string consumerGroup, Action<MessageRequestResponse> messageHandler)
        {
            _messageHandler = messageHandler;
            _topic = topic;
            _consumerGroup = consumerGroup;
            var partitionCount = await TopicList.GetPartitionCount(_client, topic);
            _cTokensForConsumerThreads = new CancellationTokenSource[partitionCount];

            var consumerGroupTable = new ConsumerGroupTable(_client);
            await consumerGroupTable.ImHere(topic, _consumerGroup, _consumerId, PartitionsChangedHandler);
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
                        if (partitions != null)
                        {
                            if (_offsetHandler == null)
                            {
                                _offsetHandler = new (SemaphoreSlim _lock, long _offset)[partitions.Length];
                                for (var i = 0; i < _offsetHandler.Length; i++)
                                {
                                    _offsetHandler[i]._lock = new SemaphoreSlim(1,1);
                                    _offsetHandler[i]._offset = -1;
                                }
                            }
                            else
                            {
                                _repartitionLock.Wait();
                                // resize array and put data still needed.
                                var oldArray = new (SemaphoreSlim _lock, long _offset)[_offsetHandler.Length];
                                _offsetHandler.CopyTo(oldArray, 0);
                                _offsetHandler = new (SemaphoreSlim _lock, long _offset)[partitions.Length];
                                for (var i = 0; i < _offsetHandler.Length; i++)
                                {
                                    _offsetHandler[i]._lock = new SemaphoreSlim(1, 1);
                                    _offsetHandler[i]._offset = -1;
                                }

                                for (var i = 0; i < _offsetHandler.Length && i < oldArray.Length; i++) 
                                    _offsetHandler[i] = oldArray[i];

                                _repartitionLock.Release();
                            }
                        }
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

        public async Task CloseConnection()
        {
            foreach (var brokerSocket in _brokerSockets)
            {
                if (brokerSocket != null) await brokerSocket.CloseConnection();
            }
        }
    }
}
