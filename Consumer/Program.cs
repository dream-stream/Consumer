using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
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
            EnvironmentVariables.SetFromEnvironmentVariables();
            EnvironmentVariables.PrintProperties();
            if (!EnvironmentVariables.IsDev)
            {
                var metricServer = new MetricServer(80);
                metricServer.Start();
            }

            Console.WriteLine($"Starting {EnvironmentVariables.ApplicationType} Consumer");
            switch (EnvironmentVariables.ApplicationType)
            {
                case "Dream-Stream":
                    await DreamStream(EnvironmentVariables.TopicName, EnvironmentVariables.ConsumerGroup);
                    break;
                case "Kafka":
                    Kafka(EnvironmentVariables.TopicName, EnvironmentVariables.ConsumerGroup);
                    break;
                case "Nats-Streaming":
                    break;
                default:
                    throw new NotImplementedException($"The method {EnvironmentVariables.ApplicationType} has not been implemented");
            }
        }

        private static void Kafka(string topicName, string consumerGroup)
        {
            var conf = KafkaConsumerConfig(consumerGroup);
            using var c = new ConsumerBuilder<Ignore, Message>(conf).Build();
            c.Subscribe(topicName);

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // prevent the process from terminating. 
                cts.Cancel();
            };

            try
            {
                while (true)
                {
                    try
                    {
                        var cr = c.Consume(cts.Token);
                        if(cr.Value != null) MessagesConsumed.WithLabels($"Kafka").Inc();
                        //Console.WriteLine($"Consumed message '{cr.Message}' at: '{cr.TopicPartitionOffset}'.");

                        if (MessagesConsumed.Value % 25000 == 1) Console.WriteLine($"Messages Consumed: {MessagesConsumed.Value}");
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Error occured: {e.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                c.Close();
            }
        }

        private static ConsumerConfig KafkaConsumerConfig(string consumerGroup)
        {
            var list = new List<string>();
            for (var i = 0; i < 3; i++) list.Add($"kf-kafka-{i}.kf-hs-kafka.default.svc.cluster.local:9093");
            var bootstrapServers = string.Join(',', list);
            var conf = new ConsumerConfig
            {
                GroupId = consumerGroup,
                BootstrapServers = bootstrapServers,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            return conf;
        }

        private static async Task DreamStream(string topic, string consumerGroup)
        {
            Console.WriteLine($"Starting Consumer subscribing to topic {topic} with consumer group {consumerGroup}");
            IConsumer consumer = new ConsumerService();

            var client = EnvironmentVariables.IsDev ? new EtcdClient("http://localhost") : new EtcdClient("http://etcd");
            await consumer.InitSockets(client);
            var consumerGroupTable = await consumer.Subscribe(topic, consumerGroup, DreamStreamMessageHandler);

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

        private static void DreamStreamMessageHandler(MessageRequestResponse msg)
        {
            try
            {
                BatchedMessagesConsumed.WithLabels($"{msg.Header.Topic}/{msg.Header.Partition}").Inc(msg.Messages.Count);

                for (var i = 0; i < msg.Messages.Count; i++)
                {
                    if (!(LZ4MessagePackSerializer.Deserialize<IMessage>(msg.Messages[i]) is MessageContainer messages))
                    {
                        Console.WriteLine("Failed!!!");
                        continue;
                    }
                    MessagesConsumed.WithLabels($"{msg.Header.Topic}/{msg.Header.Partition}").Inc(messages.Messages.Count);
                    MessagesConsumedPerSecond.Inc(messages.Messages.Count);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
