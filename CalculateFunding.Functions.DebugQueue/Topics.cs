using CalculateFunding.Functions.Calcs.ServiceBus;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Jobs;
using CalculateFunding.Models.Messages;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Results.ServiceBus;
using Microsoft.Extensions.Logging;
using CalculateFunding.Functions.CosmosDbScaling.ServiceBus;


namespace CalculateFunding.Functions.DebugQueue
{
    public static class Topics
    {
        private static readonly ILogger logger;

        static Topics()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            logger = loggerFactory.CreateLogger(typeof(Topics));
        }


        [Function("on-provider-sourcedataset-cleanup-queue")]
        public static async Task RunOnProviderSourceDatasetCleanup([QueueTrigger(ServiceBusConstants.TopicNames.ProviderSourceDatasetCleanup, Connection = "StorageConnection")] string item)
        {
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<SpecificationProviders>(item);

            using (IServiceScope scope = Functions.Results.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                try
                {
                    OnProviderResultsSpecificationCleanup function = scope.ServiceProvider.GetRequiredService<OnProviderResultsSpecificationCleanup>();

                    await function.Run(message);

                }
                catch (Exception ex)
                {
                    
                  logger.LogError(ex, $"Error while executing Results {nameof(RunOnProviderSourceDatasetCleanup)}");
                }
            }

           logger.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-edit-specification-queue")]
        public static async Task RunOnEditSpecificationEvent([QueueTrigger(ServiceBusConstants.TopicNames.EditSpecification, Connection = "StorageConnection")] string item)
        {
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<SpecificationVersionComparisonModel>(item);

            using (IServiceScope scope = Functions.Users.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                try
                {
                    Functions.Users.ServiceBus.OnEditSpecificationEvent function = scope.ServiceProvider.GetRequiredService<Functions.Users.ServiceBus.OnEditSpecificationEvent>();

                    await function.Run(message);
                }
                catch (Exception ex)
                {
                     logger.LogError(ex, $"Error while executing Users {nameof(RunOnEditSpecificationEvent)}");
                }
            }
             logger.LogInformation($"C# Queue trigger function processed: {item}");
        }


        [Function("on-data-definition-changes-queue")]
        public static async Task OnDataDefinitionChanges([QueueTrigger(ServiceBusConstants.TopicNames.DataDefinitionChanges, Connection = "StorageConnection")] string item)
        {
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<DatasetDefinitionChanges>(item);

            using (IServiceScope scope = Functions.Datasets.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                try
                {
                    Functions.Datasets.ServiceBus.OnDataDefinitionChanges function = scope.ServiceProvider.GetRequiredService<Functions.Datasets.ServiceBus.OnDataDefinitionChanges>();

                    await function.Run(message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error while executing Datasets {nameof(OnDataDefinitionChanges)}");
                }
            }

            using (IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                try
                {
                    Functions.Calcs.ServiceBus.OnDataDefinitionChanges function = scope.ServiceProvider.GetRequiredService<Functions.Calcs.ServiceBus.OnDataDefinitionChanges>();

                    await function.Run(message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error while executing Calcs {nameof(OnDataDefinitionChanges)}");
                }
            }

             logger.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-job-notification-queue")]
        [SignalROutput(HubName = JobConstants.NotificationsHubName)]
        public static async Task OnJobNotification(
            [QueueTrigger(ServiceBusConstants.TopicNames.JobNotifications, Connection = "StorageConnection")] string item)
        {
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<JobSummary>(item);

            JobSummary jobNotification = System.Text.Json.JsonSerializer.Deserialize<JobSummary>(message.Body);

            try
            {
                if (jobNotification.CompletionStatus == CompletionStatus.Succeeded && jobNotification.JobType == JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob)
                {
                    using IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
                    OnCalculationAggregationsJobCompleted function = scope.ServiceProvider.GetRequiredService<OnCalculationAggregationsJobCompleted>();

                    await function.Run(message);
                }
                else
                {
                    using IServiceScope scope = Jobs.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
                    Jobs.ServiceBus.OnJobNotification function = scope.ServiceProvider.GetRequiredService<Jobs.ServiceBus.OnJobNotification>();

                    await function.Run(message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while executing Jobs Notification Event");
            }


            using (IServiceScope scope = Functions.Notifications.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                try
                {
                    Notifications.OnNotificationEventTrigger function = scope.ServiceProvider.GetRequiredService<Notifications.OnNotificationEventTrigger>();

                    await function.Run(message);

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while executing Notification Event");
                }
            }

            using (IServiceScope scope = Functions.CosmosDbScaling.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                try
                {
                    OnScaleUpCosmosDbCollection function = scope.ServiceProvider.GetRequiredService<OnScaleUpCosmosDbCollection>();

                    await function.Run(message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while executing Scale Up Event");
                }

                logger.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }
    }
}
