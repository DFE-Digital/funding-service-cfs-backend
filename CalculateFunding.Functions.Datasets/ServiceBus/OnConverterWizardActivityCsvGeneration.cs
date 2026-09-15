using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;
using ILogger = Serilog.ILogger;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Datasets.ServiceBus
{
    public class OnConverterWizardActivityCsvGeneration : Retriable
    {
        public const string FunctionName = "on-converter-wizard-activity-csv-generation";
        private const string QueueName = ServiceBusConstants.QueueNames.ConverterWizardActivityCsvGeneration;

        public OnConverterWizardActivityCsvGeneration(
            ILogger logger,
            IConverterWizardActivityCsvGenerationGeneratorService csvGeneratorService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, csvGeneratorService, refresherProvider)
        {
        }

        /// <summary>
        /// Geenerate csv for converter wizard
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(QueueName,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey,
            IsSessionsEnabled = true)]
            ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}

