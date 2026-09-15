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
    public class OnGeneratePublishedProviderStateSummaryCsv : Retriable
    {
        private readonly ILogger _logger;
        private readonly IPublishedProviderStateSummaryCsvGenerator _csvGenerator;

        private const string FunctionName = "on-publishing-generate-published-provider-state-summary-csv";
        private const string QueueName = ServiceBusConstants.QueueNames.GeneratePublishedProviderStateSummaryCsv;

        public OnGeneratePublishedProviderStateSummaryCsv(
            ILogger logger,
            IPublishedProviderStateSummaryCsvGenerator csvGenerator,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, csvGenerator, refresherProvider)
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
