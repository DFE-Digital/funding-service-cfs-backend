using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using CalculateFunding.Common.ApiClient.Jobs;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Common.ApiClient.Models;
using CalculateFunding.Common.ApiClient.Providers;
using CalculateFunding.Common.ApiClient.Specifications;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.FeatureToggles;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.Core.Interfaces.Services;
using CalculateFunding.Services.DataImporter;
using CalculateFunding.Services.Datasets.Builders;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Datasets.Services.UnitTests;
using CalculateFunding.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NSubstitute;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Datasets.Services
{
    [TestClass]
    public class ProcessDatasetsServiceProcessDatasetTests : ProcessDatasetServiceTestsBase
    {
        private ProcessDatasetService _service;
        private IDatasetRepository _datasetRepository;
        private IVersionRepository<DatasetVersion> _datasetVersionRepository;
        private ICalcsRepository _calculationsRepository;
        private IBlobClient _blobClient;
        private ICacheProvider _cacheProvider;
        private IExcelDatasetReader _excelDatasetReader;
        private IProviderSourceDatasetsRepository _providerResultsRepository;
        private IProviderSourceDatasetVersionKeyProvider _versionKeyProvider;
        private IProvidersApiClient _providersApiClient;
        private ISpecificationsApiClient _specificationsApiClient;
        private IMessengerService _messengerService;
        private IDatasetsAggregationsRepository _datasetsAggregationsRepository;
        private IFeatureToggle _featureToggle;
        private IJobsApiClient _jobsApiClient;
        private IJobManagement _jobManagement;
        private ILogger _logger;

        private Mock<IVersionBulkRepository<ProviderSourceDatasetVersion>> _versionBulkRepository;
        private Mock<IProviderSourceDatasetBulkRepository> _providerSourceDatasetBulkRepository;

        private ServiceBusReceivedMessage _message;

        private readonly string _datasetCacheKey = $"ds-table-rows:{ProcessDatasetService.GetBlobNameCacheKey(BlobPath)}:{DataDefintionId}";
        private readonly string _datasetAggregationsCacheKey = $"{CacheKeys.DatasetAggregationsForSpecification}{SpecificationId}";
        private readonly string _calculationResultsCacheKey = $"{CacheKeys.CalculationResults}{SpecificationId}";
        private readonly string _codeContextCaheKey = $"{CacheKeys.CodeContext}{SpecificationId}";

        private string _relationshipId;
        private string _relationshipName;
        private string _upin;
        private string _ukprn;
        private string _laCode;
        private string _providerId;
        private string _jobId;

        private const string Upin = nameof(Upin);
        private const string UKPRN = nameof(UKPRN);
        private const string LaCode = nameof(LaCode);
        private const string BlobPath = "dataset-id/v1/ds.xlsx";
        private const string CreateInstructAllocationJob = JobConstants.DefinitionNames.CreateInstructAllocationJob;
        private const string CreateInstructGenerateAggregationsAllocationJob = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob;

        [TestInitialize]
        public void SetUp()
        {
            _datasetRepository = CreateDatasetsRepository();
            _datasetVersionRepository = CreateDatasetsVersionRepository();
            _calculationsRepository = CreateCalcsRepository();
            _blobClient = CreateBlobClient();
            _cacheProvider = CreateCacheProvider();
            _excelDatasetReader = CreateExcelDatasetReader();
            _providerResultsRepository = CreateProviderResultsRepository();
            _versionKeyProvider = CreateDatasetVersionKeyProvider();
            _providersApiClient = CreateProvidersApiClient();
            _specificationsApiClient = CreateSpecificationsApiClient();
            _messengerService = CreateMessengerService();
            _featureToggle = CreateFeatureToggle();
            _datasetsAggregationsRepository = CreateDatasetsAggregationsRepository();
            _jobsApiClient = CreateJobsApiClient();
            _logger = CreateLogger();
            _jobManagement = CreateJobManagement(_jobsApiClient, _logger, _messengerService);

            _versionBulkRepository = new Mock<IVersionBulkRepository<ProviderSourceDatasetVersion>>();
            _providerSourceDatasetBulkRepository = new Mock<IProviderSourceDatasetBulkRepository>();

            _service = CreateProcessDatasetService(datasetRepository: _datasetRepository,
                datasetVersionRepository: _datasetVersionRepository,
                calcsRepository: _calculationsRepository,
                blobClient: _blobClient,
                cacheProvider: _cacheProvider,
                excelDatasetReader: _excelDatasetReader,
                providerResultsRepository: _providerResultsRepository,
                versionKeyProvider: _versionKeyProvider,
                providersApiClient: _providersApiClient,
                specificationsApiClient: _specificationsApiClient,
                messengerService: _messengerService,
                featureToggle: _featureToggle,
                datasetsAggregationsRepository: _datasetsAggregationsRepository,
                jobManagement: _jobManagement,
                logger: _logger,
                versionBulkRepository: _versionBulkRepository.Object,
                providerSourceDatasetBulkRepository: _providerSourceDatasetBulkRepository.Object);

            _message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: new BinaryData(string.Empty),
                messageId: Guid.NewGuid().ToString());
            _relationshipId = NewRandomString();
            _relationshipName = NewRandomString();
            _upin = NewRandomString();
            _ukprn = NewRandomString();
            _providerId = NewRandomString();
            _laCode = NewRandomString();
            _jobId = NewRandomString();
        }

        [TestMethod]
        public void ProcessDataset_GivenNullMessage_ThrowsArgumentNullException()
        {
            _message = null;

            Func<Task> invocation = WhenTheProcessDatasetMessageIsProcessed;

            invocation
                .Should()
                .ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public async Task ProcessDataset_GivenNullPayload_DoesNoProcessing()
        {
            GivenTheMessageProperties(("jobId", "job1"));
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation
                .Should()
                .Throw<NonRetriableException>();

            await _datasetRepository
                .DidNotReceive()
                .GetDefinitionSpecificationRelationshipById(Arg.Any<string>());

            ThenTheErrorWasLogged("A null dataset was provided to ProcessData");
        }

        [TestMethod]
        public async Task ProcessDataset_GivenPayloadButNoSpecificationIdKeyInProperties_DoesNoProcessing()
        {
            GivenTheMessageProperties(("jobId", "job1"));
            AndTheMessageBody(new Dataset());
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation
                .Should()
                .Throw<NonRetriableException>();

            await _datasetRepository
                .DidNotReceive()
                .GetDefinitionSpecificationRelationshipById(Arg.Any<string>());

            ThenTheErrorWasLogged("Specification Id key is missing in ProcessDataset message properties");
        }

        [TestMethod]
        public async Task ProcessDataset_GivenPayloadButNoSpecificationIdValueInProperties_DoesNoProcessing()
        {
            GivenTheMessageProperties(("specification-id", ""), ("jobId", "job1"));
            AndTheMessageBody(new Dataset());
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation
                .Should()
                .Throw<NonRetriableException>();

            await _datasetRepository
                .DidNotReceive()
                .GetDefinitionSpecificationRelationshipById(Arg.Any<string>());

            ThenTheErrorWasLogged("A null or empty specification id was provided to ProcessData");
        }

        [TestMethod]
        public void ProcessDataset_GivenPayloadButDatasetDefinitionCouldNotBeFound_DoesNotProcess()
        {
            DatasetVersion datasetVersion = NewDatasetVersion();
            Dataset dataset = NewDataset(_ => _.WithCurrent(datasetVersion)
                .WithDefinition(NewDataDefinitionVersion(rfv => rfv.FromReference(NewReference(rf => rf.WithId(DataDefintionId))))));
            GivenTheMessageProperties(("specification-id", SpecificationId), ("relationship-id", _relationshipId), ("jobId", "job1"));
            AndTheMessageBody(dataset);
            AndTheDatasetVersion(dataset.Id, datasetVersion);
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);
            AndTheSpecification(SpecificationId, NewSpecification(_ =>
            _.WithId(SpecificationId)
            .WithProviderVersionId(ProviderVersionId)
            ));
            AndTheRelationship(_relationshipId, NewRelationship(r => r.WithCurrent(NewRelationshipVersion(_ => _.WithDatasetDefinition(NewReference(rf => rf.WithId(DataDefintionId)))
                .WithDatasetVersion(NewDatasetRelationshipVersion())))));
            AndThePopulationOfProviderSummariesForSpecification(false, false);

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation.Should()
                .Throw<NonRetriableException>();

            ThenTheErrorWasLogged($"Unable to find a data definition for id: {DataDefintionId}, for blob: {BlobPath}");
            AndAnExceptionWasLogged();
        }

        [TestMethod]
        public void ProcessDataset_GivenPayloadButBuildProjectCouldNotBeFound_DoesNotProcess()
        {
            DatasetVersion datasetVersion = NewDatasetVersion();
            Dataset dataset = NewDataset(_ => _.WithCurrent(datasetVersion)
                .WithDefinition(NewDataDefinitionVersion(rfv => rfv.FromReference(NewReference(rf => rf.WithId(DataDefintionId))))));
            GivenTheMessageProperties(("specification-id", SpecificationId), ("relationship-id", _relationshipId), ("jobId", "job1"));
            AndTheMessageBody(dataset);
            AndTheDatasetVersion(dataset.Id, datasetVersion);
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);
            AndTheSpecification(SpecificationId, NewSpecification(_ =>
            _.WithId(SpecificationId)
            .WithProviderVersionId(ProviderVersionId)
            ));
            AndTheRelationship(_relationshipId, NewRelationship(r => r.WithCurrent(NewRelationshipVersion(_ => _.WithDatasetDefinition(NewReference(rf => rf.WithId(DataDefintionId)))
                .WithDatasetVersion(NewDatasetRelationshipVersion())))));
            AndTheDatasetDefinitions(NewDatasetDefinition());
            AndThePopulationOfProviderSummariesForSpecification(false, false);

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation.Should()
                .Throw<NonRetriableException>();

            ThenTheErrorWasLogged($"Unable to find a build project for specification id: {SpecificationId}");
            AndAnExceptionWasLogged();
        }

        [TestMethod]
        public async Task ProcessDataset_GivenPayloadButBlobNotFound_DoesNotProcess()
        {
            DatasetVersion datasetVersion = NewDatasetVersion();
            Dataset dataset = NewDataset(_ => _.WithCurrent(datasetVersion)
                .WithDefinition(NewDataDefinitionVersion(rfv => rfv.FromReference(NewReference(rf => rf.WithId(DataDefintionId))))));
            GivenTheMessageProperties(("specification-id", SpecificationId), ("relationship-id", _relationshipId), ("jobId", "job1"));
            AndTheMessageBody(dataset);
            AndTheDatasetVersion(dataset.Id, datasetVersion);
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);
            AndTheSpecification(SpecificationId, NewSpecification(_ =>
            _.WithId(SpecificationId)
            .WithProviderVersionId(ProviderVersionId)
            ));
            AndTheRelationship(_relationshipId, NewRelationship(r => r.WithCurrent(NewRelationshipVersion(_ => _.WithDatasetDefinition(NewReference(rf => rf.WithId(DataDefintionId)))
                .WithDatasetVersion(NewDatasetRelationshipVersion())))));
            AndTheDatasetDefinitions(NewDatasetDefinition());
            AndTheBuildProject(SpecificationId, NewBuildProject());
            AndThePopulationOfProviderSummariesForSpecification(false, false);
            AndTheCloudBlob(BlobPath, null);

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation.Should()
                .Throw<NonRetriableException>();

            ThenTheErrorWasLogged($"Failed to find blob with path: {BlobPath}");
            AndAnExceptionWasLogged();
        }

        [TestMethod]
        public void ProcessDataset_GivenPayloadAndBlobFoundButEmptyFile_DoesNotProcess()
        {
            DatasetVersion datasetVersion = NewDatasetVersion();
            Dataset dataset = NewDataset(_ => _.WithCurrent(datasetVersion)
                .WithDefinition(NewDataDefinitionVersion(rfv => rfv.FromReference(NewReference(rf => rf.WithId(DataDefintionId))))));
            GivenTheMessageProperties(("specification-id", SpecificationId), ("relationship-id", _relationshipId), ("jobId", "job1"));
            AndTheMessageBody(dataset);
            AndTheDatasetVersion(dataset.Id, datasetVersion);
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);
            AndTheSpecification(SpecificationId, NewSpecification(_ =>
            _.WithId(SpecificationId)
            .WithProviderVersionId(ProviderVersionId)
            ));
            AndTheRelationship(_relationshipId, NewRelationship(r => r.WithCurrent(NewRelationshipVersion(_ => _.WithDatasetDefinition(NewReference(rf => rf.WithId(DataDefintionId)))
                .WithDatasetVersion(NewDatasetRelationshipVersion())))));
            AndTheDatasetDefinitions(NewDatasetDefinition());
            AndTheBuildProject(SpecificationId, NewBuildProject());
            AndThePopulationOfProviderSummariesForSpecification(false, false);

            BlobClient blobClient = NewCloudBlob();

            AndTheCloudBlob(BlobPath, blobClient);
            AndTheCloudStream(blobClient, NewStream());

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation.Should()
                .Throw<NonRetriableException>();

            ThenTheErrorWasLogged($"Invalid blob returned: {BlobPath}");
            AndAnExceptionWasLogged();
        }

        [TestMethod]
        public void ProcessDataset_GivenPayloadAndBlobFoundButNoTableResultsReturned_DoesNotProcess()
        {
            DatasetVersion datasetVersion = NewDatasetVersion();
            Dataset dataset = NewDataset(_ => _.WithCurrent(datasetVersion)
                .WithDefinition(NewDataDefinitionVersion(rfv => rfv.FromReference(NewReference(rf => rf.WithId(DataDefintionId))))));
            GivenTheMessageProperties(("specification-id", SpecificationId), ("relationship-id", _relationshipId), ("jobId", "job1"));
            AndTheMessageBody(dataset);
            AndTheDatasetVersion(dataset.Id, datasetVersion);
            AndTheJobDetails("job1", JobConstants.DefinitionNames.MapDatasetJob);
            AndTheSpecification(SpecificationId, NewSpecification(_ =>
            _.WithId(SpecificationId)
            .WithProviderVersionId(ProviderVersionId)
            ));
            AndTheRelationship(_relationshipId, NewRelationship(r => r.WithCurrent(NewRelationshipVersion(_ => _.WithDatasetDefinition(NewReference(rf => rf.WithId(DataDefintionId)))
                .WithDatasetVersion(NewDatasetRelationshipVersion())))));
            AndTheDatasetDefinitions(NewDatasetDefinition());
            AndTheBuildProject(SpecificationId, NewBuildProject());
            AndThePopulationOfProviderSummariesForSpecification(false, false);

            BlobClient blobClient = NewCloudBlob();

            AndTheCloudBlob(BlobPath, blobClient);
            AndTheCloudStream(blobClient, NewStream(new byte[100]));

            Func<Task> invocation = async () => await WhenTheProcessDatasetMessageIsProcessed();

            invocation.Should()
                .Throw<NonRetriableException>();

            ThenTheErrorWasLogged("Failed to load table result");
            AndAnExceptionWasLogged();
        }

        private CalculationResponseModel NewCalculation(Action<CalculationResponseBuilder> setUp = null)
        {
            CalculationResponseBuilder calculationResponseBuilder = new CalculationResponseBuilder();

            setUp?.Invoke(calculationResponseBuilder);

            return calculationResponseBuilder.Build();
        }

        private string NewRandomString() => new RandomString();

        private async Task WhenTheProcessDatasetMessageIsProcessed()
        {
            await _service.Run(_message);
        }

        private void GivenTheMessageProperties(params (string, string)[] properties)
        {
            IDictionary<string, object> merged = _message?.ApplicationProperties?
                .ToDictionary(kv => kv.Key, kv => kv.Value)
                ?? new Dictionary<string, object>();

            foreach ((string key, object value) in properties)
            {
                merged[key] = value;
            }

            string messageId = _message?.MessageId ?? Guid.NewGuid().ToString();
            string? sessionId = _message?.SessionId;
            string? correlationId = _message?.CorrelationId;
            string? contentType = _message?.ContentType;

            _message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: _message?.Body ?? new BinaryData(string.Empty),
                properties: merged,
                messageId: messageId,
                sessionId: sessionId,
                correlationId: correlationId,
                contentType: contentType
            );
        }

        private void AndTheMessageBody<TBody>(TBody body)
            where TBody : class
        {
            IDictionary<string, object> merged = _message?.ApplicationProperties?
                .ToDictionary(kv => kv.Key, kv => kv.Value)
                ?? new Dictionary<string, object>();

            string messageId = _message?.MessageId ?? Guid.NewGuid().ToString();
            string? sessionId = _message?.SessionId;
            string? correlationId = _message?.CorrelationId;

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(body);

            _message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(json),
                properties: merged,
                messageId: messageId,
                sessionId: sessionId,
                correlationId: correlationId,
                contentType: "application/json"
            );
        }

        private void AndTheJobDetails(string jobId, string jobDefinitionId)
        {
            _jobsApiClient.GetJobById(Arg.Is(jobId))
                .Returns(new ApiResponse<JobViewModel>(HttpStatusCode.OK, new JobViewModel() { Id = jobId, JobDefinitionId = jobDefinitionId }));
        }

        private void AndTheDatasetVersion(string datasetId, DatasetVersion datasetVersion)
        {
            _datasetVersionRepository.GetVersions(datasetId)
                .Returns(new[] { datasetVersion });
        }

        private void AndTheCloudBlob(string blobName, BlobClient blobClient)
        {
            _blobClient
                .GetBlobReferenceFromServerAsync(Arg.Is(blobName))
                .Returns(blobClient);
        }

        private void AndTheTableLoadResultsFromExcel(Stream stream, DatasetDefinition datasetDefinition, params TableLoadResult[] tableLoadResults)
        {
            _excelDatasetReader
                .Read(Arg.Is(stream), Arg.Is(datasetDefinition))
                .Returns(tableLoadResults);
        }

        private void AndTheCloudStream(BlobClient blobClient, Stream stream)
        {
            _blobClient
                .DownloadToStreamAsync(Arg.Any<BlobClient>())
                .Returns(stream);
        }

        private TableLoadResult NewTableLoadResult(Action<TableLoadResultBuilder> setUp = null)
        {
            TableLoadResultBuilder loadResultBuilder = new TableLoadResultBuilder();

            setUp?.Invoke(loadResultBuilder);

            return loadResultBuilder.Build();
        }

        private Dataset NewDataset(Action<DatasetBuilder> setUp = null)
        {
            DatasetBuilder datasetBuilder = new DatasetBuilder();

            setUp?.Invoke(datasetBuilder);

            return datasetBuilder.Build();
        }

        public DatasetRelationshipSummary NewRelationshipSummary(Action<DataRelationshipSummaryBuilder> setUp = null)
        {
            DataRelationshipSummaryBuilder relationshipSummaryBuilder = new DataRelationshipSummaryBuilder();

            setUp?.Invoke(relationshipSummaryBuilder);

            return relationshipSummaryBuilder.Build();
        }

        private DefinitionSpecificationRelationship NewRelationship(Action<DefinitionSpecificationRelationshipBuilder> setUp = null)
        {
            DefinitionSpecificationRelationshipBuilder relationshipBuilder = new DefinitionSpecificationRelationshipBuilder();

            setUp?.Invoke(relationshipBuilder);

            return relationshipBuilder.Build();
        }

        private DefinitionSpecificationRelationshipVersion NewRelationshipVersion(Action<DefinitionSpecificationRelationshipVersionBuilder> setUp = null)
        {
            DefinitionSpecificationRelationshipVersionBuilder relationshipVersionBuilder = new DefinitionSpecificationRelationshipVersionBuilder();

            setUp?.Invoke(relationshipVersionBuilder);

            return relationshipVersionBuilder.Build();
        }

        private SpecificationSummary NewSpecification(Action<ApiSpecificationSummaryBuilder> setUp = null)
        {
            ApiSpecificationSummaryBuilder relationshipBuilder = new ApiSpecificationSummaryBuilder();

            setUp?.Invoke(relationshipBuilder);

            return relationshipBuilder.Build();
        }

        private DatasetRelationshipVersion NewDatasetRelationshipVersion(Action<DatasetRelationshipVersionBuilder> setUp = null)
        {
            DatasetRelationshipVersionBuilder relationshipVersionBuilder = new DatasetRelationshipVersionBuilder()
                .WithVersion(1);

            setUp?.Invoke(relationshipVersionBuilder);

            return relationshipVersionBuilder.Build();
        }

        private DatasetVersion NewDatasetVersion(Action<DatasetVersionBuilder> setUp = null)
        {
            DatasetVersionBuilder datasetVersionBuilder = new DatasetVersionBuilder()
                .WithBlobName(BlobPath)
                .WithVersion(1);

            setUp?.Invoke(datasetVersionBuilder);

            return datasetVersionBuilder.Build();
        }

        private DatasetDefinitionVersion NewDataDefinitionVersion(Action<DatasetDefinitionVersionBuilder> setUp = null)
        {
            DatasetDefinitionVersionBuilder datasetDefinitionVersionBuilder = new DatasetDefinitionVersionBuilder();

            setUp?.Invoke(datasetDefinitionVersionBuilder);

            return datasetDefinitionVersionBuilder.Build();
        }

        private Reference NewReference(Action<ReferenceBuilder> setUp = null)
        {
            ReferenceBuilder referenceBuilder = new ReferenceBuilder();

            setUp?.Invoke(referenceBuilder);

            return referenceBuilder.Build();
        }

        public BuildProject NewBuildProject(Action<BuildProjectBuilder> setUp = null)
        {
            BuildProjectBuilder projectBuilder = new BuildProjectBuilder()
                .WithId(BuildProjectId);

            setUp?.Invoke(projectBuilder);

            return projectBuilder.Build();
        }

        private DatasetDefinition NewDatasetDefinition(Action<DatasetDefinitionBuilder> setUp = null)
        {
            DatasetDefinitionBuilder definitionBuilder = new DatasetDefinitionBuilder()
                .WithId(DataDefintionId);

            setUp?.Invoke(definitionBuilder);

            return definitionBuilder.Build();
        }

        private void AndTheRelationship(string id, DefinitionSpecificationRelationship relationship)
        {
            _datasetRepository
                .GetDefinitionSpecificationRelationshipById(id)
                .Returns(relationship);
        }

        private void AndTheSpecification(string id, SpecificationSummary specificationSummary)
        {
            _specificationsApiClient
                .GetSpecificationSummaryById(id)
                .Returns(new ApiResponse<SpecificationSummary>(HttpStatusCode.OK, specificationSummary));
        }

        private void AndTheDatasetDefinitions(params DatasetDefinition[] datasetDefinitions)
        {
            _datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(datasetDefinitions);
        }

        private void AndTheBuildProject(string specificationId, BuildProject buildProject)
        {
            _calculationsRepository
                .GetBuildProjectBySpecificationId(specificationId)
                .Returns(buildProject);
        }

        private BlobClient NewCloudBlob(Action<BlobClient> setUp = null)
        {
            BlobClient blobClient = Substitute.For<BlobClient>();

            setUp?.Invoke(blobClient);

            return blobClient;
        }

        private Stream NewStream(byte[] buffer = null)
        {
            return new MemoryStream(buffer ?? Array.Empty<byte>());
        }

        private void AndThePopulationOfProviderSummariesForSpecification(bool setCachedProviders, bool regenerated)
        {
            _providersApiClient
                .RegenerateProviderSummariesForSpecification(SpecificationId, setCachedProviders)
                .Returns(new ApiResponse<bool>(HttpStatusCode.OK, regenerated));

            _messengerService
                .ReceiveMessage(Arg.Any<string>(), Arg.Any<Predicate<JobSummary>>(), Arg.Any<TimeSpan>())
                .Returns(new JobSummary { CompletionStatus = CompletionStatus.Failed });
        }

        private void ThenTheErrorWasLogged(string errorMessage)
        {
            _logger
                .Received(1)
                .Error(errorMessage);
        }

        private void AndAnExceptionWasLogged()
        {
            _logger
                .Received(1)
                .Error(Arg.Any<Exception>(), Arg.Any<string>());
        }

        private void AndTheCachedTableLoadResults(string cacheKey, params TableLoadResult[] tableLoadResults)
        {
            _cacheProvider
                .GetAsync<TableLoadResult[]>(cacheKey)
                .Returns(tableLoadResults);
        }
    }
}
