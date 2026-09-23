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
    public class OnReferencedSpecificationReMap : Retriable
    {
        private const string FunctionName = "on-referenced-specification-remap";
        private const string QueueName = ServiceBusConstants.QueueNames.ReferencedSpecificationReMap;

        public OnReferencedSpecificationReMap(
            ILogger logger,
            IReferencedSpecificationReMapService service,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, service, refresherProvider)
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
