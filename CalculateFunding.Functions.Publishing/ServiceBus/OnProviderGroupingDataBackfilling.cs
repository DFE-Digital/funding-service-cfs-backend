using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Publishing.ServiceBus
{
    public class OnProviderGroupingDataBackfilling : Retriable
    {
        private const string FunctionName = FunctionConstants.ProviderGroupingDataBackfilling;
        private const string QueueName = ServiceBusConstants.QueueNames.ProviderGroupingDataBackfilling;

        public OnProviderGroupingDataBackfilling(
            ILogger logger,
            IProviderGroupingDataBackfillingService providerGroupingDataBackfillingService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, providerGroupingDataBackfillingService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                ServiceBusConstants.QueueNames.ProviderGroupingDataBackfilling,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
                IsSessionsEnabled = true)]
            ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
