using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.CalcEngine.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.CalcEngine.ServiceBus
{
    public class OnCalcsGenerateAllocationResults : Retriable
    {
        public const string FunctionName = "on-calcs-generate-allocations-event";
        public const string QueueName = ServiceBusConstants.QueueNames.CalcEngineGenerateAllocationResults;
        private readonly ILogger _logger;

        public OnCalcsGenerateAllocationResults(
            ILogger logger,
            ICalculationEngineService calculationEngineService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, calculationEngineService, refresherProvider)
        {
            _logger = logger;
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
            QueueName,
            IsSessionsEnabled = true,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
