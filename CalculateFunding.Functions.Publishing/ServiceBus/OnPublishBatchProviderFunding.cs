using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Publishing.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Publishing.ServiceBus
{
    public class OnPublishBatchProviderFunding : Retriable
    {
        private readonly IPublishService _publishService;
        public const string FunctionName = FunctionConstants.PublishingPublishBatchProviderFunding;
        public const string QueueName = ServiceBusConstants.QueueNames.PublishingPublishBatchProviderFunding;

        public OnPublishBatchProviderFunding(
            ILogger logger,
            IPublishService publishService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, publishService, refresherProvider)
        {
            Guard.ArgumentNotNull(publishService, nameof(publishService));

            _publishService = publishService;
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)] ServiceBusReceivedMessage message)
        {
            await base.Run(message, async () =>
            {
                await _publishService.PublishProviderFundingResults(message, batched: true);
            });
        }
    }
}
