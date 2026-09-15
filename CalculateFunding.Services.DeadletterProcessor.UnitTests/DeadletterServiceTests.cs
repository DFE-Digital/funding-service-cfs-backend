using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Services.DeadletterProcessor;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;


namespace CalculateFunding.Services.Core.Services
{
    [TestClass]
    public class DeadletterServiceTests
    {
        [TestMethod]
        public async Task ProcessDeadLetteredMessage_GivenMessageButNoJobId_LogsAnErrorAndDoesNotUpdadeJobLog()
        {
            // Arrange: received message with NO "jobId" app property
            var message = CreateReceivedMessage(jobId: null);

            IJobManagement jobManagement = CreateJobManagement();
            ILogger logger = CreateLogger();

            IDeadletterService service = CreateDeadletterService(jobManagement, logger);

            // Act
            await service.Process(message);

            // Assert
            logger
                .Received(1)
                .Error(Arg.Is("Missing job id from dead lettered message"));

            await jobManagement
                .DidNotReceive()
                .AddJobLog(Arg.Any<string>(), Arg.Any<JobLogUpdateModel>());
        }

        [TestMethod]
        public async Task ProcessDeadLetteredMessage_GivenMessageButAddingLogCausesException_LogsAnError()
        {
            // Arrange
            const string jobId = "job-id-1";
            var message = CreateReceivedMessage(jobId);

            IJobManagement jobManagement = CreateJobManagement();
            jobManagement
                .When(x => x.AddJobLog(Arg.Is(jobId), Arg.Any<JobLogUpdateModel>()))
                .Do(_ => { throw new Exception("boom"); });

            ILogger logger = CreateLogger();

            IDeadletterService service = CreateDeadletterService(jobManagement, logger);

            // Act
            await service.Process(message);

            // Assert
            logger
                .Received(1)
                .Error(Arg.Any<Exception>(), Arg.Is($"Failed to add a job log for job id '{jobId}'"));
        }

        [TestMethod]
        public async Task ProcessDeadLetteredMessage_GivenMessageAndLogIsUpdated_LogsInformation()
        {
            // Arrange
            const string jobId = "job-id-1";

            var jobLog = new JobLog { Id = "job-log-id-1" };
            var message = CreateReceivedMessage(jobId);

            IJobManagement jobManagement = CreateJobManagement();
            jobManagement
                .AddJobLog(Arg.Is(jobId), Arg.Any<JobLogUpdateModel>())
                .Returns(jobLog);

            ILogger logger = CreateLogger();

            IDeadletterService service = CreateDeadletterService(jobManagement, logger);

            // Act
            await service.Process(message);

            // Assert
            logger
                .Received(1)
                .Information(Arg.Is($"A new job log was added to inform of a dead lettered message with job log id '{jobLog.Id}' on job with id '{jobId}'"));
        }

        #region Helpers

        private static ServiceBusReceivedMessage CreateReceivedMessage(string? jobId)
        {
            // Build a "received" message for tests using the official model factory.
            // NOTE: The 'properties' parameter populates ApplicationProperties.
            //       Body can be anything; here we include a small JSON payload.
            var appProps = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(jobId))
            {
                appProps["jobId"] = jobId; 
            }

            return ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(JsonSerializer.Serialize(new { note = "test" })),
                contentType: "application/json",
                properties: appProps
            );
        }

        private static IDeadletterService CreateDeadletterService(IJobManagement jobManagement, ILogger logger)
            => new DeadletterService(jobManagement, logger);

        private static IJobManagement CreateJobManagement() => Substitute.For<IJobManagement>();

        private static ILogger CreateLogger() => Substitute.For<ILogger>();

        #endregion
    }
}
