using Azure.Messaging.ServiceBus;
using CalculateFunding.Models.Jobs;
using CalculateFunding.Services.Core.Constants;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Notifications.UnitTests
{
    [TestClass]
    public partial class NotificationServiceTests
    {
        [TestMethod]
        public async Task OnNotificationEvent_WhenParentJobWithoutSpecificationIdIsCreated_ThenSignalRMessageActionsAdded()
        {
            // Arrange
            NotificationService service = CreateService();

            JobSummary jobNotification = new JobSummary()
            {
                CompletionStatus = null,
                JobId = JobId,
                JobType = "test",

            };

            string json = JsonConvert.SerializeObject(jobNotification);


            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: new BinaryData(json),
              messageId: Guid.NewGuid().ToString()
              );

            List<SignalRMessageAction> generatedMessages = CreateSignalRMessageActionCollector();

            // Act
            await service.OnNotificationEvent(message);

            // Assert
            generatedMessages
                .Received(2)
                .Add(Arg.Any<SignalRMessageAction>());

            generatedMessages
                .Received(1)
                .Add(
                Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.All));

            generatedMessages
               .Received(1)
               .Add(
               Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.ParentJobs));
        }

        [TestMethod]
        public async Task OnNotificationEvent_WhenParentJobWithSpecificationIdIsCreated_ThenSignalRMessageActionsAdded()
        {
            // Arrange
            NotificationService service = CreateService();

            JobSummary jobNotification = new JobSummary()
            {
                CompletionStatus = null,
                JobId = JobId,
                JobType = "test",
                SpecificationId = SpecificationId,

            };

            string json = JsonConvert.SerializeObject(jobNotification);


            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: new BinaryData(json),
              messageId: Guid.NewGuid().ToString()
              );

            List<SignalRMessageAction> generatedMessages = CreateSignalRMessageActionCollector();

            // Act
            await service.OnNotificationEvent(message);

            // Assert
            generatedMessages
                .Received(3)
                .Add(Arg.Any<SignalRMessageAction>());

            generatedMessages
                .Received(1)
                .Add(
                Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.All));

            generatedMessages
               .Received(1)
               .Add(
               Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.ParentJobs));

            generatedMessages
               .Received(1)
               .Add(
               Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == $"{JobConstants.NotificationChannels.SpecificationPrefix}{SpecificationId.Replace("-", "")}"));
        }

        [TestMethod]
        public async Task OnNotificationEvent_WhenChildJobWithSpecificationIdIsCreated_ThenSignalRMessageActionsAdded()
        {
            // Arrange
            NotificationService service = CreateService();

            JobSummary jobNotification = new JobSummary()
            {
                CompletionStatus = null,
                JobId = JobId,
                JobType = "test",
                SpecificationId = SpecificationId,
                ParentJobId = "parentJobId1",
            };

            string json = JsonConvert.SerializeObject(jobNotification);


            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: new BinaryData(json),
              messageId: Guid.NewGuid().ToString()
              );

            List<SignalRMessageAction> generatedMessages = CreateSignalRMessageActionCollector();

            // Act
            await service.OnNotificationEvent(message);

            // Assert
            generatedMessages
                .Received(2)
                .Add(Arg.Any<SignalRMessageAction>());

            generatedMessages
                .Received(1)
                .Add(
                Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.All));

            generatedMessages
               .Received(1)
               .Add(
               Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == $"{JobConstants.NotificationChannels.SpecificationPrefix}{SpecificationId.Replace("-", "")}"));

            generatedMessages
               .Received(0)
               .Add(
               Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.ParentJobs));
        }

        [TestMethod]
        public async Task OnNotificationEvent_WhenJobIsCreated_ThenJobNotificationPropertiesAreSet()
        {
            // Arrange
            NotificationService service = CreateService();

            JobSummary jobNotification = new JobSummary()
            {
                CompletionStatus = CompletionStatus.Succeeded,
                JobId = JobId,
                JobType = "test",
                SpecificationId = SpecificationId,
                ParentJobId = "parentJobId1",
                InvokerUserDisplayName = "invokerDisplayName",
                InvokerUserId = "InvokerUserId",
                ItemCount = 52,
                Outcome = "Outcome text",
                OverallItemsFailed = 2,
                OverallItemsProcessed = 23,
                OverallItemsSucceeded = 21,
                RunningStatus = RunningStatus.InProgress,
                StatusDateTime = new DateTimeOffset(2018, 12, 2, 5, 6, 7, 8, TimeSpan.Zero),
                SupersededByJobId = "jobId",
                Trigger = new Trigger()
                {
                    EntityId = "triggerEntity",
                    EntityType = "triggerEntityType",
                    Message = "message"
                },
                Created = new DateTimeOffset(new DateTime(2020, 1, 1))
            };


            string json = JsonConvert.SerializeObject(jobNotification);


            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: new BinaryData(json),
              messageId: Guid.NewGuid().ToString()
              );

            List<SignalRMessageAction> generatedMessages = CreateSignalRMessageActionCollector();

            // Act
            await service.OnNotificationEvent(message);

            // Assert
            generatedMessages
                .Received(1)
                .Add(
                Arg.Is<SignalRMessageAction>(
                    c => c.Target == JobConstants.NotificationsTargetFunction &&
                    c.Arguments.Length == 1 &&
                    c.Arguments.First() != null &&
                    c.GroupName == JobConstants.NotificationChannels.All &&
                  ((JobSummary)c.Arguments.First()).CompletionStatus == CompletionStatus.Succeeded &&
                  ((JobSummary)c.Arguments.First()).InvokerUserDisplayName == "invokerDisplayName" &&
                  ((JobSummary)c.Arguments.First()).InvokerUserId == "InvokerUserId" &&
                  ((JobSummary)c.Arguments.First()).ItemCount == 52 &&
                  ((JobSummary)c.Arguments.First()).Outcome == "Outcome text" &&
                  ((JobSummary)c.Arguments.First()).OverallItemsFailed == 2 &&
                  ((JobSummary)c.Arguments.First()).OverallItemsProcessed == 23 &&
                  ((JobSummary)c.Arguments.First()).OverallItemsSucceeded == 21 &&
                  ((JobSummary)c.Arguments.First()).RunningStatus == RunningStatus.InProgress &&
                  ((JobSummary)c.Arguments.First()).StatusDateTime == new DateTimeOffset(2018, 12, 2, 5, 6, 7, 8, TimeSpan.Zero) &&
                  ((JobSummary)c.Arguments.First()).SupersededByJobId == "jobId" &&
                  ((JobSummary)c.Arguments.First()).Trigger.EntityId == "triggerEntity" &&
                  ((JobSummary)c.Arguments.First()).Trigger.EntityType == "triggerEntityType" &&
                  ((JobSummary)c.Arguments.First()).Trigger.Message == "message" &&
                  ((JobSummary)c.Arguments.First()).Created == new DateTimeOffset(new DateTime(2020, 1, 1))
                  ));
        }

        [TestMethod]
        public void OnNotificationEvent_WhenJobNotificationBodyIsNull_ThenErrorThrown()
        {
            // Arrange
            NotificationService service = CreateService();

            JobSummary jobNotification = null;

            string json = JsonConvert.SerializeObject(jobNotification);


            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: new BinaryData(json),
              messageId: Guid.NewGuid().ToString()
              );

            // Act
            Func<Task> func = new Func<Task>(async () => { await service.OnNotificationEvent(message); });

            // Assert
            func
                .Should()
                .Throw<InvalidOperationException>()
                .WithMessage("Job notificiation was null");
        }
    }
}
