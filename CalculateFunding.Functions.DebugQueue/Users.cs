using Azure.Messaging.ServiceBus;
using CalculateFunding.Functions.Users.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Users
    {
        private static readonly ILogger log;

        static Users()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            log = loggerFactory.CreateLogger(typeof(Users));
        }

        [Function("users-on-reindex-users-queue")]
        public static async Task RunReIndexUser([QueueTrigger(ServiceBusConstants.QueueNames.UsersReIndexUsers, Connection = "StorageConnection")] string item)
        {
            using IServiceScope scope = Functions.Users.Startup.RegisterComponents(new ServiceCollection()).CreateScope();

            ServiceBusReceivedMessage message = Helpers.ConvertToMessage<ServiceBusReceivedMessage>(item);

            OnReIndexUsersEvent function = scope.ServiceProvider.GetRequiredService<OnReIndexUsersEvent>();

            await function.Run(message);

           log.LogInformation($"C# Queue trigger function processed: {item}");
        }
    }
}
