﻿using System;
using System.Threading;
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
        private static readonly Counter BatchedMessagesConsumed = Metrics.CreateCounter("batched_messages_consumed", "Number of batched messages consumed.");
        private static readonly Counter MessagesConsumed = Metrics.CreateCounter("messages_consumed", "Number of messages consumed.");
        private static readonly Counter NoNewMessages = Metrics.CreateCounter("no_new_messages", "Number of times the message \"No new messages\" has been received.");
        private static readonly Gauge MessagesConsumedPerSecond = Metrics.CreateGauge("messages_consumed_per_second", 
            "Messages consumed for the current second.");

        private static long testCounter;

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
            await consumer.Subscribe(topic, consumerGroup, MessageHandler);

            while (true)
            {
                Console.WriteLine($"Messages consumed: {MessagesConsumed.Value}");
                Console.WriteLine($"Messages Consumed Per Second: {MessagesConsumedPerSecond.Value}");
                Console.WriteLine($"Batched Messages consumed: {BatchedMessagesConsumed.Value}");
                MessagesConsumedPerSecond.Set(0);
                await Task.Delay(1000);
            }
        }

        private static void MessageHandler(MessageRequestResponse msg)
        {
            BatchedMessagesConsumed.Inc(msg.Messages.Count);

            for (var i = 0; i < msg.Messages.Count; i++)
            {
                if (!(LZ4MessagePackSerializer.Deserialize<IMessage>(msg.Messages[i]) is MessageContainer messages))
                {
                    Console.WriteLine("Failed!!!");
                    continue;
                }

                Interlocked.Add(ref testCounter, messages.Messages.Count);

                MessagesConsumed.Inc(messages.Messages.Count);
                MessagesConsumedPerSecond.Inc(messages.Messages.Count);
            }


            //foreach (var serializedMessages in msg.Messages)
            //{
            //    if (!(LZ4MessagePackSerializer.Deserialize<IMessage>(serializedMessages) is MessageContainer messages)) continue;
                
            //    MessagesConsumed.Inc(messages.Messages.Count);
            //    MessagesConsumedPerSecond.Inc(messages.Messages.Count);
            //}

            //if(Math.Abs(MessagesConsumed.Value%1000) < 1) Console.WriteLine("1000 messages");

            

            //msg.Messages.ForEach(message => message.Print());
        }
    }
}
