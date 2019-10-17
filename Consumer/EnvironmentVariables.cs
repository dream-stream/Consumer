using System;

namespace Consumer
{
    public class EnvironmentVariables
    {
        public static bool IsDev { get; } = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    }
}