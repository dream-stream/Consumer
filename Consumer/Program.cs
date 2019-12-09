using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using Consumer.Services;
using dotnet_etcd;
using MessagePack;
using Prometheus;

namespace Consumer
{
    internal class Program
    {
        private static readonly Counter BatchedMessagesConsumed = Metrics.CreateCounter("batched_messages_consumed", "Number of batched messages consumed.", new CounterConfiguration
        {
            LabelNames = new[] { "TopicPartition" }
        });
        private static readonly Counter MessagesConsumed = Metrics.CreateCounter("messages_consumed", "Number of messages consumed.", new CounterConfiguration
        {
            LabelNames = new[] { "TopicPartition" }
        });
        //private static readonly Counter NoNewMessages = Metrics.CreateCounter("no_new_messages", "Number of times the message \"No new messages\" has been received.");
        private static readonly Gauge MessagesConsumedPerSecond = Metrics.CreateGauge("messages_consumed_per_second", "Messages consumed for the current second.");


        static async Task Main()
        {
            if(!EnvironmentVariables.IsDev)
            {
                var metricServer = new MetricServer(80);
                metricServer.Start();
            }

            const string topic = "Topic3";
            const string consumerGroup = "Anders-Is-A-Noob";
            //var timer = new Timer(_ =>
            //{
            //    Console.WriteLine($"Messages consumed: {MessagesConsumedPerSecond.Value}");
            //    MessagesConsumedPerSecond.Set(0);

            //}, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            Console.WriteLine($"Starting Consumer subscribing to topic {topic} with consumer group {consumerGroup}");

            IConsumer consumer = new ConsumerService(new MessageProcessor());
            
            var client = EnvironmentVariables.IsDev ? new EtcdClient("http://localhost") : new EtcdClient("http://etcd");
            await consumer.InitSockets(client);
            var consumerGroupTable = await consumer.Subscribe(topic, consumerGroup, MessageHandler);

            while (true)
            {
                Console.WriteLine($"Messages consumed: {MessagesConsumed.Value}");
                Console.WriteLine($"Batched Messages consumed: {BatchedMessagesConsumed.Value}");
                Console.WriteLine($"Messages Consumed Per 5 sec Second: {MessagesConsumedPerSecond.Value}");

                consumerGroupTable.LeaseKeepAlive(null);

                MessagesConsumedPerSecond.Set(0);
                await Task.Delay(5000);
            }
        }

        private static void MessageHandler(MessageRequestResponse msg)
        {
            try
            {
                BatchedMessagesConsumed.Inc(msg.Messages.Count);

                for (var i = 0; i < msg.Messages.Count; i++)
                {
                    if (!(LZ4MessagePackSerializer.Deserialize<IMessage>(msg.Messages[i]) is MessageContainer messages))
                    {
                        Console.WriteLine("Failed!!!");
                        continue;
                    }
                    MessagesConsumed.WithLabels($"{msg.Header.Topic}/{msg.Header.Partition}").Inc(messages.Messages.Count);
                    MessagesConsumedPerSecond.WithLabels($"{msg.Header.Topic}/{msg.Header.Partition}").Inc(messages.Messages.Count);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
