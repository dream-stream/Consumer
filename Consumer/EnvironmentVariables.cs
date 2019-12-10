using System;

namespace Consumer
{
    public class EnvironmentVariables
    {
        public static void SetFromEnvironmentVariables()
        {
            ApplicationType = Environment.GetEnvironmentVariable("APPLICATION_TYPE") ?? "Dream-Stream";
            TopicName = Environment.GetEnvironmentVariable("TOPIC_NAME") ?? "Topic3";
            ConsumerGroup = Environment.GetEnvironmentVariable("CONSUMER_GROUP") ?? "MyConsumerGroup";
            IsDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        public static void PrintProperties()
        {
            var obj = typeof(EnvironmentVariables);
            foreach (var descriptor in obj.GetProperties())
                Console.WriteLine($"{descriptor.Name}={descriptor.GetValue(obj)}");
        }

        public static bool IsDev { get; set; }
        public static string ApplicationType { get; set; }
        public static string TopicName { get; set; }
        public static string ConsumerGroup { get; set; }
    }
}