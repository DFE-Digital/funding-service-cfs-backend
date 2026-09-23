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
    public class OnApproveAllProviderFunding : Retriable
    {
        public const string FunctionName = FunctionConstants.PublishingApproveAllProviderFunding;
        public const string QueueName = ServiceBusConstants.QueueNames.PublishingApproveAllProviderFunding;

        public OnApproveAllProviderFunding(
            ILogger logger,
            IApproveService approveService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, approveService, refresherProvider)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
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
