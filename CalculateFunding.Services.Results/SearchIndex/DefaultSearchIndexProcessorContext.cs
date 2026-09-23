using Azure.Messaging.ServiceBus;
using CalculateFunding.Services.Results.Interfaces;

namespace CalculateFunding.Services.Results.SearchIndex
{
    public class DefaultSearchIndexProcessorContext : ISearchIndexProcessorContext
    {
        public DefaultSearchIndexProcessorContext(ServiceBusReceivedMessage message)
        {
            Message = message;
            DegreeOfParallelism = 45;
        }

        public ServiceBusReceivedMessage Message { get; }

        public int DegreeOfParallelism { get; }
    }
}
