using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using Consumer.Services;
using dotnet_etcd;

namespace Consumer
{
    internal class Program
    {
        static async Task Main()
        {
            const string topic = "Topic2";
            const string consumerGroup = "Nicklas-Is-A-Noob";

            Console.WriteLine($"Starting Consumer subscribing to topic {topic} with consumer group {consumerGroup}");

            IConsumer consumer = new ConsumerService(new MessageProcessor());
            
            var client = EnvironmentVariables.IsDev ? new EtcdClient("http://localhost") : new EtcdClient("http://etcd");
            await consumer.InitSockets(client);
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
                    msg.Messages.ForEach(message => message.Print());
                    break;
                case NoNewMessage _:
                    Console.WriteLine($"No new messages");
                    break;
                default:
                    throw new Exception("Unknown message type");
            }
            //container.Messages.ForEach(message => message.Print());
        }
    }
}
