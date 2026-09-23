using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Specs.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;

namespace CalculateFunding.Functions.Specs.ServiceBus
{
    public class OnDeleteSpecifications : Retriable
    {
        private readonly ISpecificationsService _specificationsService;
        public const string FunctionName = "on-delete-specifications";
        private const string QueueName = ServiceBusConstants.QueueNames.DeleteSpecifications;

        public OnDeleteSpecifications(
            ILogger logger,
            ISpecificationsService specificationsService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, specificationsService, refresherProvider)
        {
            Guard.ArgumentNotNull(specificationsService, nameof(specificationsService));

            _specificationsService = specificationsService;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(QueueName, 
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await Run(message,
                async () =>
                {
                    await _specificationsService.DeleteSpecification(message);
                });
        }
    }
}
