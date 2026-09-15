using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Processing.Functions;
using CalculateFunding.Services.Users.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Serilog;
namespace CalculateFunding.Functions.Users.ServiceBus
{
    public class OnReIndexUsersEvent: Retriable
    {
        public const string FunctionName = "users-on-reindex-users";
        private const string QueueName = ServiceBusConstants.QueueNames.UsersReIndexUsers;

        public OnReIndexUsersEvent(
            ILogger logger,
            IUserIndexingService userIndexingService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false)
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, userIndexingService, refresherProvider)
        {
        }

        /// <summary>
        /// Reindexing all Users
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
