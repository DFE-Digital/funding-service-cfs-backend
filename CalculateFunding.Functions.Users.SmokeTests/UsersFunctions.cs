using CalculateFunding.Common.Models;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Users.Interfaces;
using CalculateFunding.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;
using System.Threading.Tasks;
using CalculateFunding.Functions.Users.ServiceBus;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Tests.Common.Helpers;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using System.Text;

namespace CalculateFunding.Functions.Users.SmokeTests
{
    [TestClass]
    public class TestEngineFunctions : SmokeTestBase
    {
        private static ILogger _logger;
        private static IFundingStreamPermissionService _fundingStreamPermissionService;
        private static IUserProfileProvider _userProfileProvider;
        private static IUserIndexingService _userIndexingService;

        [ClassInitialize]
        public static void SetupTests(TestContext tc)
        {
            SetupTests("users");

            _logger = CreateLogger();
            _fundingStreamPermissionService = CreateFundingStreamPermissionService();
            _userProfileProvider = CreateUserProfileProvider();
            _userIndexingService = CreateUserIndexingService();
        }

        [TestMethod]
        public async Task OnEditSpecificationEvent_SmokeTestSucceeds()
        {
            OnEditSpecificationEvent onEditSpecificationEvent = new OnEditSpecificationEvent(_logger,
                _fundingStreamPermissionService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                 _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                 IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.TopicSubscribers.UpdateUsersForEditSpecification,
                async(ServiceBusReceivedMessage smokeResponse) => await onEditSpecificationEvent.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))),
                ServiceBusConstants.TopicNames.EditSpecification);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnReIndexUsersEvent_SmokeTestSucceeds()
        {
            OnReIndexUsersEvent onReIndexUsersEvent = new OnReIndexUsersEvent(_logger,
                _userIndexingService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                 _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                 IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.UsersReIndexUsers,
                async (ServiceBusReceivedMessage smokeResponse) => await onReIndexUsersEvent.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))));

            response
                .Should()
                .NotBeNull();
        }

        private static ILogger CreateLogger()
        {
            return Substitute.For<ILogger>();
        }

        private static IFundingStreamPermissionService CreateFundingStreamPermissionService()
        {
            return Substitute.For<IFundingStreamPermissionService>();
        }

        private static IUserProfileProvider CreateUserProfileProvider()
        {
            return Substitute.For<IUserProfileProvider>();
        }

        private static IUserIndexingService CreateUserIndexingService()
        {
            return Substitute.For<IUserIndexingService>();
        }
    }
}
