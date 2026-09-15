using System;
using System.Threading.Tasks;
using CalculateFunding.Services.Jobs.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CalculateFunding.Services.Jobs
{
    public partial class JobServiceTests
    {
        [TestMethod]
        public void GetJobsCount_WhenSpecificationIdIsNull_ThrowArgumentNullException()
        {
            // Arrange
            string specificationId = null;
            string jobDefinitionId = NewRandomString();
            string runningStatus = NewRandomString();
            string completionStatus = NewRandomString();

            IJobService service = CreateJobService();

            // Act
            Func<Task> action = () => service.GetJobsCountByJobDefinitionIdAndStatus(specificationId, jobDefinitionId, runningStatus, completionStatus);

            // Assert
            action
                .Should()
                .Throw<ArgumentNullException>()
                .And
                .ParamName
                .Should()
                .Be("specificationId");
        }

        [TestMethod]
        public void GetJobsCount_WhenJobDefinitionIdIsNull_ThrowArgumentNullException()
        {
            // Arrange
            string specificationId = NewRandomString();
            string jobDefinitionId = null;
            string runningStatus = NewRandomString();
            string completionStatus = NewRandomString();

            IJobService service = CreateJobService();

            // Act
            Func<Task> action = () => service.GetJobsCountByJobDefinitionIdAndStatus(specificationId, jobDefinitionId, runningStatus, completionStatus);

            // Assert
            action
                .Should()
                .Throw<ArgumentNullException>()
                .And
                .ParamName
                .Should()
                .Be("jobDefinitionId");
        }

        [TestMethod]
        public void GetJobsCount_WhenRunningStatusIsNull_ThrowArgumentNullException()
        {
            // Arrange
            string specificationId = NewRandomString();
            string jobDefinitionId = NewRandomString();
            string runningStatus = null;
            string completionStatus = NewRandomString();

            IJobService service = CreateJobService();

            // Act
            Func<Task> action = () => service.GetJobsCountByJobDefinitionIdAndStatus(specificationId, jobDefinitionId, runningStatus, completionStatus);

            // Assert
            action
                .Should()
                .Throw<ArgumentNullException>()
                .And
                .ParamName
                .Should()
                .Be("runningStatus");
        }

        [TestMethod]
        public void GetJobsCount_WhenCompletionStatusIsNull_ThrowArgumentNullException()
        {
            // Arrange
            string specificationId = NewRandomString();
            string jobDefinitionId = NewRandomString();
            string runningStatus = NewRandomString();
            string completionStatus = null;

            IJobService service = CreateJobService();

            // Act
            Func<Task> action = () => service.GetJobsCountByJobDefinitionIdAndStatus(specificationId, jobDefinitionId, runningStatus, completionStatus);

            // Assert
            action
                .Should()
                .Throw<ArgumentNullException>()
                .And
                .ParamName
                .Should()
                .Be("completionStatus");
        }

        [TestMethod]
        public async Task GetJobsCount_WhenGivenValidInputs_ReturnCount()
        {
            // Arrange
            string specificationId = NewRandomString();
            string jobDefinitionId = NewRandomString();
            string runningStatus = NewRandomString();
            string completionStatus = NewRandomString();

            IJobRepository jobRepository = CreateJobRepository();
            jobRepository
                .GetJobsCountByJobDefinitionIdAndStatus
                (Arg.Is(specificationId), 
                Arg.Is(jobDefinitionId), 
                Arg.Is(runningStatus), 
                Arg.Is(completionStatus))
                .Returns(3);

            IJobService service = CreateJobService(jobRepository);

            // Act
            IActionResult result = await service.GetJobsCountByJobDefinitionIdAndStatus(
                specificationId,
                jobDefinitionId,
                runningStatus,
                completionStatus);

            // Assert
            OkObjectResult okResult = result
                .Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            okResult.Value.Should().Be(3);
        }

    }
}
