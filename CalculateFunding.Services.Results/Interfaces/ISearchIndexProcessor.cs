using Azure.Messaging.ServiceBus;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Results.Interfaces
{
    public interface ISearchIndexProcessor
    {
        Task Process(ServiceBusReceivedMessage message);
        string IndexWriterType { get; }
    }
}
