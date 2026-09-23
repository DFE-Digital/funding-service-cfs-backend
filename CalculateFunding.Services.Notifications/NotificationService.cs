using System;
using System.Threading.Tasks;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Jobs;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Notifications.Interfaces;
using CalculateFunding.Services.Processing;
using Microsoft.Azure.Functions.Worker;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;


namespace CalculateFunding.Services.Notifications
{
    public class NotificationService : ProcessingService, INotificationService
    {
        public override Task Process(ServiceBusReceivedMessage message)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<SignalRMessageAction>> OnNotificationEvent(ServiceBusReceivedMessage message)
        {
            Guard.ArgumentNotNull(message, nameof(message));
            List<SignalRMessageAction> signalRMessages = new List<SignalRMessageAction>();

            JobSummary jobNotification = message.GetPayloadAsInstanceOf<JobSummary>();

            if (jobNotification == null)
            {
                throw new InvalidOperationException("Job notificiation was null");
            }

            // Send to all notifications channel
            signalRMessages.Add(
                    new SignalRMessageAction(JobConstants.NotificationsTargetFunction)
                    {
                        GroupName = JobConstants.NotificationChannels.All,
                        Arguments = new[] { jobNotification }
                    });

            if (!string.IsNullOrWhiteSpace(jobNotification.SpecificationId))
            {
                // Send to individual specifications group
                signalRMessages.Add(
                    new SignalRMessageAction(JobConstants.NotificationsTargetFunction)
                    {
                        GroupName = $"{JobConstants.NotificationChannels.SpecificationPrefix}{jobNotification.SpecificationId.Replace("-", "")}",
                        Arguments = new[] { jobNotification }
                    });
            }

            if (string.IsNullOrWhiteSpace(jobNotification.ParentJobId))
            {
                // Send to parent jobs only group
                signalRMessages.Add(
                    new SignalRMessageAction(JobConstants.NotificationsTargetFunction)
                    {
                        GroupName = JobConstants.NotificationChannels.ParentJobs,
                        Arguments = new[] { jobNotification }
                    });
            }

            return signalRMessages;
        }
    }
}
