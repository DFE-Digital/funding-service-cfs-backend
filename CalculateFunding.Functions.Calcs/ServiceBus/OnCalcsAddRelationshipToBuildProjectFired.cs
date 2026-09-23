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
    public class CalcsAddRelationshipToBuildProject : Retriable
    {
        private IBuildProjectsService _buildProjectsService;
        public const string FunctionName = "on-calcs-add-data-relationship";
        public const string QueueName = ServiceBusConstants.QueueNames.UpdateBuildProjectRelationships;

        public CalcsAddRelationshipToBuildProject(
            ILogger logger,
            IBuildProjectsService buildProjectsService,
            IMessengerService messengerService,
            IUserProfileProvider userProfileProvider,
            IConfigurationRefresherProvider refresherProvider,
            bool useAzureStorage = false) 
            : base(logger, messengerService, FunctionName, QueueName, useAzureStorage, userProfileProvider, buildProjectsService, refresherProvider)
        {
        }

        [Function(FunctionName)]
        public async Task Run([ServiceBusTrigger(QueueName, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] ServiceBusReceivedMessage message)
        {
            await base.Run(message, async() =>
            {
                await _buildProjectsService.UpdateBuildProjectRelationships(message);
            });
        }
    }
}
