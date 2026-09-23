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
    public class OnApproveBatchProviderFunding : Retriable
    {
        private readonly IApproveService _approveService;
        public const string FunctionName = FunctionConstants.PublishingApproveBatchProviderFunding;
        public const string QueueName = ServiceBusConstants.QueueNames.PublishingApproveBatchProviderFunding;

        public OnApproveBatchProviderFunding(
            ILogger logger,
            IApproveService approveService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, approveService, refresherProvider)
        {
            Guard.ArgumentNotNull(approveService, nameof(approveService));

            _approveService = approveService;
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)] ServiceBusReceivedMessage message)
        {
            await base.Run(message, async () =>
            {
                await _approveService.ApproveResults(message, batched: true);
            });
        }
    }
}
