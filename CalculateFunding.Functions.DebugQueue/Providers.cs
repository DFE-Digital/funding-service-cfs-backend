using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Providers.ServiceBus;
using CalculateFunding.Functions.Providers.Timer;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Providers
    {
        private const string Every2Minute = "*/2 * * * *";
        private static readonly ILogger log;

        static Providers()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Providers));
        }

        [Function("on-populate-scopedproviders-event-queue")]
        public static async Task RunOnPopulateScopedProvidersEventTrigger([QueueTrigger(ServiceBusConstants.QueueNames.PopulateScopedProviders, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPopulateScopedProvidersEventTrigger function = scope.ServiceProvider.GetRequiredService<OnPopulateScopedProvidersEventTrigger>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function("on-populate-scopedproviders-event-failure-queue")]
        public static async Task RunOnPopulateScopedProvidersEventTriggerFailure([QueueTrigger(ServiceBusConstants.QueueNames.PopulateScopedProvidersPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnPopulateScopedProvidersEventTriggerFailure function = scope.ServiceProvider.GetRequiredService<OnPopulateScopedProvidersEventTriggerFailure>();

            await function.Run(message);

             log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ProviderSnapshotDataLoad)]
        public static async Task RunOnProviderSnapshotDataLoadEventTrigger([QueueTrigger(ServiceBusConstants.QueueNames.ProviderSnapshotDataLoad, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnProviderSnapshotDataLoadEventTrigger function = scope.ServiceProvider.GetRequiredService<OnProviderSnapshotDataLoadEventTrigger>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.ProviderSnapshotDataLoadPoisoned)]
        public static async Task RunOnProviderSnapshotDataLoadEventTriggerFailure([QueueTrigger(ServiceBusConstants.QueueNames.ProviderSnapshotDataLoadPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnProviderSnapshotDataLoadEventTriggerFailure function = scope.ServiceProvider.GetRequiredService<OnProviderSnapshotDataLoadEventTriggerFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.TrackLatest)]
        public static async Task RunOnTrackLatestEventTrigger([QueueTrigger(ServiceBusConstants.QueueNames.TrackLatest, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnTrackLatestEventTrigger function = scope.ServiceProvider.GetRequiredService<OnTrackLatestEventTrigger>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.TrackLatestPoisoned)]
        public static async Task RunOnTrackLatestEventTriggerFailure([QueueTrigger(ServiceBusConstants.QueueNames.TrackLatestPoisonedLocal, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<string>(item);

            OnTrackLatestEventTriggerFailure function = scope.ServiceProvider.GetRequiredService<OnTrackLatestEventTriggerFailure>();

            await function.Run(message);

            log.LogInformation($"C# Queue trigger function processed: {item}");
        }

        [Function(FunctionConstants.NewProviderVersionCheck)]
        public static async Task RunOnNewProviderVersionCheck([TimerTrigger(Every2Minute, RunOnStartup = true)] TimerInfo timerInfo)
        {
            using IServiceScope scope = Functions.Providers.Startup.RegisterComponents(new ServiceCollection()).CreateScope();
            OnNewProviderVersionCheck function = scope.ServiceProvider.GetRequiredService<OnNewProviderVersionCheck>();

            await function.Run(timerInfo);

            log.LogInformation($"C# Queue trigger function processed for providerversioncheck");
        }
    }
}
