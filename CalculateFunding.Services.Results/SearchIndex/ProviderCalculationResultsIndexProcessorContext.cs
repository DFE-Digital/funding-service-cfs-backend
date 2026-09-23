using CalculateFunding.Services.Results.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Extensions;
using System.Collections.Generic;
using System.Linq;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Services.Results.SearchIndex
{
    public class ProviderCalculationResultsIndexProcessorContext : ISearchIndexProcessorContext
    {
        public ProviderCalculationResultsIndexProcessorContext(ServiceBusReceivedMessage message)
        {
            Guard.ArgumentNotNull(message, nameof(message));

            Message = message;
            SpecificationId = Message.GetUserProperty<string>("specification-id");
            SpecificationName = Message.GetUserProperty<string>("specification-name");
            ProviderIds = Message.GetPayloadAsInstanceOf<IEnumerable<string>>()?.ToList();

            Guard.ArgumentNotNull(SpecificationId, nameof(SpecificationId));
            Guard.ArgumentNotNull(SpecificationName, nameof(SpecificationName));
            Guard.ArgumentNotNull(ProviderIds, nameof(ProviderIds));
        }

        public ServiceBusReceivedMessage Message { get; }

        public int DegreeOfParallelism { get; set; }

        public string SpecificationId { get; }
        public string SpecificationName { get; }
        public IEnumerable<string> ProviderIds { get;}
    }
}
