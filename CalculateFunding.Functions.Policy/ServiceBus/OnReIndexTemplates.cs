using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Policy.Interfaces;
using CalculateFunding.Services.Processing.Functions;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
using Microsoft.Azure.Functions.Worker;

namespace CalculateFunding.Functions.Policy.ServiceBus
{
    public class OnReIndexTemplates : Retriable
    {
        private const string FunctionName = "on-policy-reindex-templates";
        private const string QueueName = ServiceBusConstants.QueueNames.PolicyReIndexTemplates;

        public OnReIndexTemplates(ILogger logger,
            ITemplatesReIndexerService templatesReIndexerService,
            IMessengerService messengerService,
             IUserProfileProvider userProfileProvider,
             IConfigurationRefresherProvider refresherProvider,
             bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, templatesReIndexerService, refresherProvider)
        {
        }

        /// <summary>
        /// reindexing the templates 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(
                QueueName,
                Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message);
        }
    }
}
