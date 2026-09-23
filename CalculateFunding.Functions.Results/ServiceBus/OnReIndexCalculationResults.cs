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
    public class OnReIndexCalculationResults : Retriable
    {
        public const string FunctionName = "on-reindex-calculation-results";
        private const string QueueName = ServiceBusConstants.QueueNames.ReIndexCalculationResultsIndex;

        public OnReIndexCalculationResults(
            ILogger logger,
            IProviderCalculationResultsReIndexerService indexerService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, indexerService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(ServiceBusConstants.QueueNames.ReIndexCalculationResultsIndex, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
