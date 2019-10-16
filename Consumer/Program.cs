using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using Consumer.Services;

namespace Consumer
{
    internal class Program
    {
        static async Task Main()
        {
            IConsumer consumer = new ConsumerService(new BrokerSocket(), new MessageProcessor());

            await consumer.Connect("ws://localhost:5000/ws");

            await consumer.Subscribe("Some Topic", "Anders-Is-A-Noob", MessageHandler);
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
