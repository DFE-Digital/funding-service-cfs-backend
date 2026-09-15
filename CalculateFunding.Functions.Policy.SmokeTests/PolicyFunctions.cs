using System.Threading.Tasks;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Functions.Policy.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Policy.Interfaces;
using CalculateFunding.Tests.Common;
using CalculateFunding.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;
using Newtonsoft.Json;
using System.Text;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Policy.SmokeTests
{
    [TestClass]
    public class PolicyFunctions : SmokeTestBase
    {
        private static ILogger _logger;
        private static ITemplatesReIndexerService _templatesReIndexerService;
        private static IUserProfileProvider _userProfileProvider;

        [ClassInitialize]
        public static void SetupTests(TestContext tc)
        {
            SetupTests("datasets");

            _logger = CreateLogger();

            _templatesReIndexerService = CreateTemplatesReIndexerService();
            _userProfileProvider = CreateUserProfileProvider();
        }

        [TestMethod]
        public async Task OnReIndexTemplates_SmokeTestSucceeds()
        {
            OnReIndexTemplates onReIndexTemplates = new OnReIndexTemplates(_logger,
                _templatesReIndexerService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PolicyReIndexTemplates,
                 async (ServiceBusReceivedMessage smokeResponse) => await onReIndexTemplates.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        private static ILogger CreateLogger()
        {
            return Substitute.For<ILogger>();
        }

        private static ITemplatesReIndexerService CreateTemplatesReIndexerService()
        {
            return Substitute.For<ITemplatesReIndexerService>();
        }

        private static IUserProfileProvider CreateUserProfileProvider()
        {
            return Substitute.For<IUserProfileProvider>();
        }
    }
}
