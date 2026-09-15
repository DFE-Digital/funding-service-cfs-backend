using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Providers.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Providers.ServiceBus
{
    public class OnPopulateScopedProvidersEventTrigger : Retriable
    {
        private const string FunctionName = FunctionConstants.PopulateScopedProviders;
        private const string QueueName = ServiceBusConstants.QueueNames.PopulateScopedProviders;

        public OnPopulateScopedProvidersEventTrigger(
            ILogger logger,
            IScopedProvidersService scopedProviderService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, scopedProviderService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
