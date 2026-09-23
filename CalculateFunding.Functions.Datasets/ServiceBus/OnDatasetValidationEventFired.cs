using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Datasets.ServiceBus
{
    public class OnDatasetValidationEvent : Retriable
    {
        private const string FunctionName = "on-dataset-validation-event";
        private const string QueueName = ServiceBusConstants.QueueNames.ValidateDataset;

        public OnDatasetValidationEvent(
            ILogger logger,
            IDatasetService datasetService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, datasetService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(QueueName, 
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
