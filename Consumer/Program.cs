using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using Consumer.Services;
using dotnet_etcd;
using Prometheus;

namespace Consumer
{
    internal class Program
    {
        private static readonly Counter BatchedMessagesConsumed = Metrics.CreateCounter("batched_messages_consumed", "Number of batched messages consumed.");
        private static readonly Counter MessagesConsumed = Metrics.CreateCounter("messages_consumed", "Number of messages consumed.");

        static async Task Main()
        {
            if(!EnvironmentVariables.IsDev)
            {
                var metricServer = new MetricServer(80);
                metricServer.Start();
            }

            const string topic = "Topic2";
            const string consumerGroup = "Nicklas-Is-A-Noob";

            Console.WriteLine($"Starting Consumer subscribing to topic {topic} with consumer group {consumerGroup}");

            IConsumer consumer = new ConsumerService(new MessageProcessor());
            
            var client = EnvironmentVariables.IsDev ? new EtcdClient("http://localhost") : new EtcdClient("http://etcd");
            await consumer.InitSockets(client);
            // Added delay to give it time to watch before creating.
            await consumer.Subscribe(topic, consumerGroup, MessageHandler);

            while (true)
            {
                await Task.Delay(10000);
            }
        }

        private static void MessageHandler(IMessage container)
        {
            switch (container)
            {
                case MessageContainer msg:
                    Console.WriteLine($"Topic: {msg.Header.Topic}, Partition: {msg.Header.Partition}\n Received messages but not printing");
                    BatchedMessagesConsumed.Inc();
                    msg.Messages.ForEach(message => MessagesConsumed.Inc());
                    //msg.Messages.ForEach(message => message.Print());
                    break;
                case NoNewMessage _:
                    // TODO Maybe comment in again or make some kind of delay, but this is SPAMMING
                    //Console.WriteLine($"No new messages");
                    break;
                default:
                    throw new Exception("Unknown message type");
            }
            //container.Messages.ForEach(message => message.Print());
        }
    }
}
