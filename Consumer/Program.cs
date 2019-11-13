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
        private static readonly Counter NoNewMessages = Metrics.CreateCounter("no_new_messages", "Number of times the message \"No new messages\" has been received.");

        static async Task Main()
        {
            if(!EnvironmentVariables.IsDev)
            {
                var metricServer = new MetricServer(80);
                metricServer.Start();
            }

            const string topic = "Topic3";
            const string consumerGroup = "Nicklas-Is-A-Noob";

            Console.WriteLine($"Starting Consumer subscribing to topic {topic} with consumer group {consumerGroup}");

            IConsumer consumer = new ConsumerService(new MessageProcessor());
            
            var client = EnvironmentVariables.IsDev ? new EtcdClient("http://localhost") : new EtcdClient("http://etcd");
            await consumer.InitSockets(client);
            await consumer.Subscribe(topic, consumerGroup, MessageHandler);

            while (true) await Task.Delay(10000);
        }

        private static void MessageHandler(MessageRequestResponse msg)
        {
            BatchedMessagesConsumed.Inc();
            MessagesConsumed.Inc(msg.Messages.Count);

            if(Math.Abs(MessagesConsumed.Value%1000) < 1) Console.WriteLine("1000 messages");

            //msg.Messages.ForEach(message => message.Print());
        }
    }
}
