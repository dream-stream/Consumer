using System;
using System.Threading;
using System.Threading.Tasks;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;

namespace Consumer.Services
{
    public class ConsumerGroupTable
    {
        // TODO back to 10
        private const int LeaseTtl = 10;
        private readonly EtcdClient _client;
        private long _leaseId;
        private string _key;
        private Timer _timer;
        public const string Prefix = "ConsumerGroup/";

        public ConsumerGroupTable(EtcdClient client)
        {
            _client = client;
        }

        public async Task ImHere(string topic, string consumerGroup, Guid id, Action<WatchEvent[]> partitionsChangedHandler)
        {
            _key = $"{Prefix}{topic}/{consumerGroup}/{id}";
            await ImHere(_key);
            _client.Watch(_key, partitionsChangedHandler);
        }



        private async Task ImHere(string key)
        {
            var leaseGrantRequest = new LeaseGrantRequest
            {
                TTL = LeaseTtl
            };

            var leaseGrantResponse = await _client.LeaseGrantAsync(leaseGrantRequest);
            _leaseId = leaseGrantResponse.ID;
            await _client.PutAsync(new PutRequest
            {
                Lease = leaseGrantResponse.ID,
                Key = ByteString.CopyFromUtf8(key),
            });

            _timer = new Timer(LeaseKeepAlive, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        private void LeaseKeepAlive(object state)
        {
            _client.LeaseKeepAlive(new LeaseKeepAliveRequest{ID = _leaseId}, KeepAliveResponseHandler, CancellationToken.None);
        }

        private async void KeepAliveResponseHandler(LeaseKeepAliveResponse leaseKeepAliveResponse)
        {
            //TODO maybe the handling for this should be different in the consumer? Throw and crash?
            if (leaseKeepAliveResponse.TTL == LeaseTtl) return;
            Console.WriteLine("Failed the KeepAliveResponse, disposing the current timer and starting a new");
            await _timer.DisposeAsync();
            await ImHere(_key);
        }
    }
}