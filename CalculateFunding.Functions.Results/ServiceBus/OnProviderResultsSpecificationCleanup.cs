using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Results.ServiceBus
{
    public class OnProviderResultsSpecificationCleanup : Retriable
    {
        public const string FunctionName = "on-provider-results-specification-cleanup";

        public OnProviderResultsSpecificationCleanup(
            ILogger logger,
            IResultsService resultsService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, $"{ServiceBusConstants.TopicNames.ProviderSourceDatasetCleanup}/{ServiceBusConstants.TopicSubscribers.CleanupCalculationResultsForSpecificationProviders}", useAzureStorage, userProfileProvider, resultsService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            ServiceBusConstants.TopicNames.ProviderSourceDatasetCleanup,
            ServiceBusConstants.TopicSubscribers.CleanupCalculationResultsForSpecificationProviders,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
