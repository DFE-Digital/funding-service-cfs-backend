using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Calcs.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.Calcs.ServiceBus
{
    public class OnDataDefinitionChanges : Retriable
    {
        public const string FunctionName = "on-data-definition-changes";

        public OnDataDefinitionChanges(
            ILogger logger,
            IDatasetDefinitionFieldChangesProcessor datasetDefinitionFieldChangesProcessor,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, $"{ServiceBusConstants.TopicNames.DataDefinitionChanges}/{ServiceBusConstants.TopicSubscribers.UpdateCalculationFieldDefinitionProperties}", useAzureStorage, userProfileProvider, datasetDefinitionFieldChangesProcessor, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            ServiceBusConstants.TopicNames.DataDefinitionChanges,
            ServiceBusConstants.TopicSubscribers.UpdateCalculationFieldDefinitionProperties,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
