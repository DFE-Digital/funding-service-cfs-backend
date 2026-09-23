
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Tests.Common.Helpers
{
    public static class MessageExtensions
    {
        public static void AddUserProperties(this ServiceBusMessage message, params (string, string)[] properties)
        {
            foreach ((string, string) property in properties)
            {
                message.ApplicationProperties.Add(property.Item1, property.Item2);
            }
        }
    }
}