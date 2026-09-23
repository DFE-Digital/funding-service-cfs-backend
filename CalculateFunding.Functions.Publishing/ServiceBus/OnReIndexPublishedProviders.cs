using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Publishing;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Publishing.ServiceBus
{
    public class OnReIndexPublishedProviders : Retriable
    {
        public const string FunctionName = "on-publishing-reindex-published-providers";
        private const string QueueName = ServiceBusConstants.QueueNames.PublishingReIndexPublishedProviders;

        public OnReIndexPublishedProviders(
            ILogger logger,
            IPublishedProviderReIndexerService publishedProviderReIndexerService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, publishedProviderReIndexerService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                QueueName,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey)]
            ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}