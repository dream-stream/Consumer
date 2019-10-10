using System;
using System.Threading.Tasks;
using Consumer.Models.Messages;
using Consumer.Services;

namespace Consumer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IConsumer consumer = new ConsumerService(new SocketService(), new MessageProcessor());

            //TODO Join consumer group

            await consumer.Connect("ws://localhost:5000/ws");

            await consumer.Subscribe("Foo", MessageHandler);
        }

        private static void MessageHandler(MessageContainer container)
        {
            container.Messages.ForEach(message => message.Print());
        }
    }
}
