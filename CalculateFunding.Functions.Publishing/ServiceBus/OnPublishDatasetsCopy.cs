using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Publishing.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Publishing.ServiceBus
{
    public class OnPublishDatasetsCopy : Retriable
    {
        public const string FunctionName = FunctionConstants.PublishingDatasetsDataCopy;
        public const string QueueName = ServiceBusConstants.QueueNames.PublishingDatasetsDataCopy;

        public OnPublishDatasetsCopy(
            ILogger logger,
            IDatasetsDataCopyService datasetsDataCopyService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, datasetsDataCopyService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
