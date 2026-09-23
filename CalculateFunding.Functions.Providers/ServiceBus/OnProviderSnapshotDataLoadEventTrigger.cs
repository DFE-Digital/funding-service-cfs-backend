using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using Serilog;
using CalculateFunding.Services.Providers.Interfaces;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Providers.ServiceBus
{
    public class OnProviderSnapshotDataLoadEventTrigger : Retriable
    {
        public const string FunctionName = FunctionConstants.ProviderSnapshotDataLoad;
        public const string QueueName = ServiceBusConstants.QueueNames.ProviderSnapshotDataLoad;

        public OnProviderSnapshotDataLoadEventTrigger(
            ILogger logger,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IProviderSnapshotDataLoadService providerSnapshotDataLoadService,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, providerSnapshotDataLoadService, refresherProvider)
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
