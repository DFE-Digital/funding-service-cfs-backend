using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Functions.Publishing.ServiceBus;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Publishing;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;
using System.Threading.Tasks;
using CalculateFunding.Services.Publishing.SqlExport;
using CalculateFunding.Tests.Common.Helpers;
using CalculateFunding.Services.Publishing.FundingManagement;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using Newtonsoft.Json;
using System.Text;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Functions.Publishing.SmokeTests
{
    [TestClass]
    public class PublishingFunctions : SmokeTestBase
    {
        private static ILogger _logger;
        private static IApproveService _approveService;
        private static IRefreshService _refreshService;
        private static IPublishService _publishService;
        private static IPublishIntegrityCheckService _publishIntegrityCheckService;
        private static IPublishedProviderReIndexerService _publishedProviderReIndexerService;
        private static IPublishingV3ToSqlMigrator _publishingV3ToSqlMigrator;
        private static IDeletePublishedProvidersService _deletePublishedProvidersService;
        private static IUserProfileProvider _userProfileProvider;
        private static ISqlImportService _sqlImportService;
        private static IDatasetsDataCopyService _datasetsDataCopyService;
        private static IReleaseProvidersToChannelsService _releaseProvidersToChannelsService;


        [ClassInitialize]
        public static void SetupTests(TestContext tc)
        {
            SetupTests("calcengine");

            _logger = CreateLogger();
            _approveService = CreateApproveService();
            _publishService = CreatePublishService();
            _refreshService = CreateRefreshService();
            _publishedProviderReIndexerService = CreatePublishedProviderReIndexerService();
            _publishingV3ToSqlMigrator = CreatePublishingV3ToSqlMigrator();
            _deletePublishedProvidersService = CreateDeletePublishedProvidersService();
            _userProfileProvider = CreateUserProfileProvider();
            _sqlImportService = Substitute.For<ISqlImportService>();
            _datasetsDataCopyService = CreateDatasetsDataCopyService();
            _releaseProvidersToChannelsService = CreateReleaseProvidersToChannelsService();
        }
        
        [TestMethod]
        public async Task OnRunSqlImport_SmokeTestSucceeds()
        {
            OnRunSqlImport onRunSqlImport = new OnRunSqlImport(_logger,
                _sqlImportService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
            _userProfileProvider,
            AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);
            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingRunSqlImport,
                async(ServiceBusReceivedMessage smokeResponse) => await onRunSqlImport.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnRunReleasedSqlImport_SmokeTestSucceeds()
        {
            OnRunReleasedSqlImport onRunSqlImport = new OnRunReleasedSqlImport(_logger,
                _sqlImportService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingRunReleasedSqlImport,
                async (ServiceBusReceivedMessage smokeResponse) => await onRunSqlImport.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnApproveAllProviderFunding_SmokeTestSucceeds()
        {
            OnApproveAllProviderFunding onApproveSpecificationFunding = new OnApproveAllProviderFunding(_logger,
                _approveService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingApproveAllProviderFunding,
                async(ServiceBusReceivedMessage smokeResponse) => await onApproveSpecificationFunding.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnApproveBatchProviderFunding_SmokeTestSucceeds()
        {
            OnApproveBatchProviderFunding onApproveProviderFunding = new OnApproveBatchProviderFunding(_logger,
                _approveService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingApproveBatchProviderFunding,
                async(ServiceBusReceivedMessage smokeResponse) => await onApproveProviderFunding.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnPublishAllProviderFunding_SmokeTestSucceeds()
        {
            OnPublishAllProviderFunding onPublishFunding = new OnPublishAllProviderFunding(_logger,
                _publishService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingPublishAllProviderFunding,
                async(ServiceBusReceivedMessage smokeResponse) => await onPublishFunding.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);


            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnPublishBatchProviderFunding_SmokeTestSucceeds()
        {
            OnPublishBatchProviderFunding onPublishFunding = new OnPublishBatchProviderFunding(_logger,
                _publishService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingPublishBatchProviderFunding,
                async(ServiceBusReceivedMessage smokeResponse) => await onPublishFunding.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnDatasetsDataCopy_SmokeTestSucceeds()
        {
            OnPublishDatasetsCopy onPublishDatasetsCopy = new OnPublishDatasetsCopy(_logger,
                _datasetsDataCopyService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingDatasetsDataCopy,
                async (ServiceBusReceivedMessage smokeResponse) => await onPublishDatasetsCopy.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        public async Task OnPublishIntegrityCheck_SmokeTestsSucceeds()
        {
            OnPublishIntegrityCheck onPublishIntegrityCheck = new OnPublishIntegrityCheck(_logger,
                _publishIntegrityCheckService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishIntegrityCheck,
                async(ServiceBusReceivedMessage smokeResponse) => await onPublishIntegrityCheck.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnRefreshFunding_SmokeTestSucceeds()
        {
            OnRefreshFunding onRefreshFunding = new OnRefreshFunding(_logger,
                _refreshService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingRefreshFunding,
                async(ServiceBusReceivedMessage smokeResponse) => await onRefreshFunding.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))), useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnReIndexPublishedProvider_SmokeTestSucceeds()
        {
            OnReIndexPublishedProviders onReIndexPublishedProviders = new OnReIndexPublishedProviders(_logger,
                _publishedProviderReIndexerService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingReIndexPublishedProviders,
                async(ServiceBusReceivedMessage smokeResponse) => await onReIndexPublishedProviders.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))));

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnReleaseManagementDataMigration_SmokeTestSucceeds()
        {
            OnReleaseManagementDataMigration onReleaseManagementDataMigration = new OnReleaseManagementDataMigration(_logger,
                _publishingV3ToSqlMigrator,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingReIndexPublishedProviders,
                async (ServiceBusReceivedMessage smokeResponse) => await onReleaseManagementDataMigration.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))),
                useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnDeletePublishedProviders_SmokeTestSucceeds()
        {
            OnDeletePublishedProviders onDeletePublishedProvider = new OnDeletePublishedProviders(_logger,
                _deletePublishedProvidersService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.DeletePublishedProviders,
                async(ServiceBusReceivedMessage smokeResponse) => await onDeletePublishedProvider.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))));

            response
                .Should()
                .NotBeNull();
        }

        [TestMethod]
        public async Task OnReleaseProvidersToChannels_SmokeTestSucceeds()
        {
            OnReleaseProvidersToChannels onReleaseProvidersToChannels = new OnReleaseProvidersToChannels(_logger,
                _releaseProvidersToChannelsService,
                Services.BuildServiceProvider().GetRequiredService<IMessengerService>(),
                _userProfileProvider,
                AppConfigurationHelper.CreateConfigurationRefresherProvider(),
                IsDevelopment);

            SmokeResponse response = await RunSmokeTest(ServiceBusConstants.QueueNames.PublishingReleaseProvidersToChannels,
                async (ServiceBusReceivedMessage smokeResponse) => await onReleaseProvidersToChannels.Run(JsonConvert.DeserializeObject<ServiceBusReceivedMessage>(Encoding.UTF8.GetString(smokeResponse.Body))),
                useSession: true);

            response
                .Should()
                .NotBeNull();
        }

        private static ILogger CreateLogger()
        {
            return Substitute.For<ILogger>();
        }
        
        private static IApproveService CreateApproveService()
        {
            return Substitute.For<IApproveService>();
        }

        private static IRefreshService CreateRefreshService()
        {
            return Substitute.For<IRefreshService>();
        }

        private static IPublishService CreatePublishService()
        {
            return Substitute.For<IPublishService>();
        }

        private static IDatasetsDataCopyService CreateDatasetsDataCopyService()
        {
            return Substitute.For<IDatasetsDataCopyService>();
        }

        private static IReleaseProvidersToChannelsService CreateReleaseProvidersToChannelsService()
        {
            return Substitute.For<IReleaseProvidersToChannelsService>();
        }

        private static IPublishedProviderReIndexerService CreatePublishedProviderReIndexerService()
        {
            return Substitute.For<IPublishedProviderReIndexerService>();
        }

        private static IPublishingV3ToSqlMigrator CreatePublishingV3ToSqlMigrator()
        {
            return Substitute.For<IPublishingV3ToSqlMigrator>();
        }

        private static IDeletePublishedProvidersService CreateDeletePublishedProvidersService()
        {
            return Substitute.For<IDeletePublishedProvidersService>();
        }

        private static IUserProfileProvider CreateUserProfileProvider()
        {
            return Substitute.For<IUserProfileProvider>();
        }
    }
}
