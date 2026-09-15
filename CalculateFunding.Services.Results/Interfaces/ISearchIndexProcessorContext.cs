using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Services.Results.Interfaces
{
    public interface ISearchIndexProcessorContext
    {
        public ServiceBusReceivedMessage Message { get; }

        public int DegreeOfParallelism { get; }
    }
}
