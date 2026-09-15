using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using CalculateFunding.Common.ApiClient.Models;
using CalculateFunding.Common.ApiClient.Providers;
using CalculateFunding.Common.ApiClient.Providers.Models;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.DataImporter.Validators.Models;
using CalculateFunding.Services.Datasets.Interfaces;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;
using BadRequestObjectResult = Microsoft.AspNetCore.Mvc.BadRequestObjectResult;
using ValidationResult = FluentValidation.Results.ValidationResult;
using Azure.Messaging.ServiceBus;
using Azure;
using CalculateFunding.Common.ApiClient.Specifications;

namespace CalculateFunding.Services.Datasets.Services
{
    [TestClass]
    public class DatasetsServiceValidateDatasetTests : DatasetServiceTestsBase
    {
        private IValidator<DatasetUploadValidationModel> datasetUploadValidator;

        [TestMethod]
        public async Task ValidateDataset_GivenNullModel_ReturnsBadRequest()
        {
            //Arrange
            ILogger logger = CreateLogger();

            DatasetService service = CreateDatasetService(logger: logger);

            // Act
            IActionResult result = await service.ValidateDataset(null, null, null);

            // Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("Null model name was provided to ValidateDataset"));
        }

        [TestMethod]
        public async Task ValidateDataset_GivenInvalidModel_ReturnsBadRequest()
        {
            //Arrange
            GetDatasetBlobModel model = new GetDatasetBlobModel();

            ILogger logger = CreateLogger();

            ValidationResult validationResult = new ValidationResult(new[]{
                    new ValidationFailure("prop1", "any error")
                });

            IValidator<GetDatasetBlobModel> validator = CreateGetDatasetBlobModelValidator(validationResult);


            DatasetService service = CreateDatasetService(logger: logger, getDatasetBlobModelValidator: validator);

            // Act
            IActionResult result = await service.ValidateDataset(model, null, null);

            // Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task ValidateDataset_GivenModelButBlobNotFound_ReturnsPreConditionFailed()
        {
            //Arrange
            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx"));

            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";

            ILogger logger = CreateLogger();

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .GetBlobReferenceFromServerAsync(Arg.Is(blobPath))
                .Returns((BlobClient)null);
            
            blobClient
                .BlobExistsAsync(Arg.Any<string>())
                .Returns(true);

            DatasetService service = CreateDatasetService(logger: logger, blobClient: blobClient);

            // Act
            IActionResult result = await service.ValidateDataset(model, null, null);

            // Assert
            result
                .Should()
                .BeOfType<PreconditionFailedResult>()
                .Which
                .Value
                .Should()
                .Be($"Failed to find blob with path: {blobPath}");

            logger
                .Received(1)
                .Error($"Failed to find blob with path: {blobPath}");
        }

        [TestMethod]
        public async Task ValidateDataset_GivenModelButAndBlobFoundButBlobHasNoData_ReturnsPreConditionFailed()
        {
            //Arrange
            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx"));

            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId }
            };

            // Create a v12 BlobClient for the test blob (no network calls performed because IBlobClient is mocked)
            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(new byte[0]);

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .BlobExistsAsync(Arg.Is(blobPath))
                .Returns(true);
            blobClient
                .GetBlobReferenceFromServerAsync(Arg.Is(blobPath))
                .Returns(blob);
            blobClient
               .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
               .Returns(memoryStream);
            blobClient
               .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
               .Returns(Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metaData), null)));

            IEnumerable<DatasetDefinition> datasetDefinitions = Enumerable.Empty<DatasetDefinition>();

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));

            DatasetService service = CreateDatasetService(logger: logger, blobClient: blobClient, datasetRepository: datasetRepository);

            // Act
            IActionResult result = await service.ValidateDataset(model, null, null);

            // Assert
            result
                .Should()
                .BeOfType<PreconditionFailedResult>()
                .Which
                .Value
                .Should()
                .Be($"Blob {blobPath} contains no data");

            logger
                .Received(1)
                .Error(Arg.Is($"Blob {blobPath} contains no data"));
        }

        [TestMethod]
        public async Task ValidateDataset_GivenModelButAndBlobDataDefinitionNotFound_ReturnsPreConditionFailed()
        {
            // Arrange
            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx"));

            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId }
            };

            // Create a v12 BlobClient for the test blob (no network calls performed because IBlobClient is mocked)
            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .BlobExistsAsync(Arg.Is(blobPath))
                .Returns(true);
            blobClient
                .GetBlobReferenceFromServerAsync(Arg.Is(blobPath))
                .Returns(blob);
            blobClient
               .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
               .Returns(memoryStream);
            blobClient
               .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
               .Returns(Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metaData), null)));

            IEnumerable<DatasetDefinition> datasetDefinitions = Enumerable.Empty<DatasetDefinition>();

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));

            DatasetService service = CreateDatasetService(logger: logger, blobClient: blobClient, datasetRepository: datasetRepository);

            // Act
            IActionResult result = await service.ValidateDataset(model, null, null);

            // Assert
            result
                .Should()
                .BeOfType<PreconditionFailedResult>()
                .Which
                .Value
                .Should()
                .Be($"Unable to find a data definition for id: {DataDefinitionId}, for blob: {blobPath}");

            logger
                .Received(1)
                .Error(Arg.Is($"Unable to find a data definition for id: {DataDefinitionId}, for blob: {blobPath}"));
        }

        [TestMethod]
        public async Task OnValidateDataset_GivenTableResultsContainsOneError_EnsuresDatasetValidationModelIsWrittenToCache()
        {
            //Arrange
            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx"));

            string uploadedBlobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";
            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.v{model.Version}.xlsx";

            ServiceBusReceivedMessage message = GetValidateDatasetMessage(model);

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId },
                { "fundingStreamId", FundingStreamId },
                { "name", "test-dataset-name" }
            };

            IPolicyRepository policyRepository = CreatePolicyRepository();
            policyRepository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .CopyBlobAsync(Arg.Is(uploadedBlobPath), Arg.Is(blobPath))
                .Returns(new BlockBlobClient(blob.Uri));
            blobClient
                .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
                .Returns(memoryStream);
            blobClient
                .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
                .Returns(Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metaData), null)));

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = DataDefinitionId,
                FundingStreamId = FundingStreamId,
                ValidateProviders = true,
                ValidateProvidersByYearRange = 2,
                TableDefinitions = new List<TableDefinition>
                {
                    new TableDefinition
                    {
                        FieldDefinitions = new List<FieldDefinition>
                        {
                            new FieldDefinition { IdentifierFieldType = IdentifierFieldType.UKPRN }
                        }
                    }
                }
            };

            IEnumerable<DatasetDefinition> datasetDefinitions = new[]
            {
                datasetDefinition
            };

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository
                .GetRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(new[] { "spec-1" }));

            datasetRepository
                .GetDatasetsByQuery(Arg.Any<Expression<Func<DocumentEntity<Dataset>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<Dataset>>(Enumerable.Empty<Dataset>()));

            List<DatasetValidationError> errors = new List<DatasetValidationError>
            {
                new DatasetValidationError { ErrorMessage = "error" }
            };

            ValidationResult validationResult = new ValidationResult(new[]{
                new ValidationFailure("prop1", "any error")
            });

            datasetUploadValidator = CreateDatasetUploadValidator(validationResult);

            IEnumerable<TableLoadResult> tableLoadResults = new[]
            {
                new TableLoadResult{ GlobalErrors = errors }
            };

            ApiResponse<ProviderVersion> providerVersionResponse = new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
            {
                Providers = new[]
                {
                    new Common.ApiClient.Providers.Models.Provider()
                },
                TargetDate = DateTimeOffset.Now
            });

            IProvidersApiClient providersApiClient = CreateProvidersApiClient();
            providersApiClient
                .GetCurrentProvidersForFundingStream(FundingStreamId)
                .Returns(providerVersionResponse);
            providersApiClient
                .GetProvidersByVersion(Arg.Any<string>())
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
                {
                    Providers = new[] { new Common.ApiClient.Providers.Models.Provider() },
                    TargetDate = DateTimeOffset.Now
                }));

            ISpecificationsApiClient specificationsApiClient = CreateSpecificationsApiClient();
            specificationsApiClient
                .GetDistinctProviderVersionIdsFromSpecifications(Arg.Any<IEnumerable<string>>())
                .Returns(new ApiResponse<IEnumerable<string>>(HttpStatusCode.OK, new[] { "providerVersion1" }));

            ICacheProvider cacheProvider = CreateCacheProvider();

            IMapper mapper = CreateMapper();

            DatasetService service = CreateDatasetService(
                logger: logger,
                blobClient: blobClient,
                datasetRepository: datasetRepository,
                datasetUploadValidator: datasetUploadValidator,
                cacheProvider: cacheProvider,
                providersApiClient: providersApiClient,
                mapper: mapper,
                policyRepository: policyRepository,
                specificationsApiClient: specificationsApiClient);

            // Act
            Func<Task> action = async () => { await service.Run(message); };

            // Assert
            action.Should().Throw<NonRetriableException>().WithMessage("Failed validation - The data source file does not match the schema rules;");
            await cacheProvider
                .Received(1)
                .SetAsync(
                    Arg.Is($"{CacheKeys.DatasetValidationStatus}:{message.ApplicationProperties["operation-id"]}"), 
                    Arg.Is<DatasetValidationStatusModel>(v =>
                        v.OperationId == message.ApplicationProperties["operation-id"].ToString() &&
                        v.ValidationFailures.Count == 2));
        }

        [TestMethod]
        public async Task OnValidateDataset_GivenProvidersApiFailed_ThrowsRetriableException()
        {
            //Arrange
            string errorMessage = $"Failed to fetch provider target period for funding stream {FundingStreamId} with status code: BadRequest";

            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx")
                .WithVersion(2));

            string uploadedBlobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";
            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.v{model.Version}.xlsx";

            ServiceBusReceivedMessage message = GetValidateDatasetMessage(model);

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId },
                { "fundingStreamId", FundingStreamId },
                { "name", "test-dataset-name" }
            };

            IPolicyRepository policyRepository = CreatePolicyRepository();
            policyRepository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .CopyBlobAsync(Arg.Is(uploadedBlobPath), Arg.Is(blobPath))
                .Returns(new BlockBlobClient(blob.Uri));
            blobClient
                .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
                .Returns(memoryStream);
            blobClient
                .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
                .Returns(Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metaData), null)));

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = DataDefinitionId,
                FundingStreamId = FundingStreamId,
                ValidateProviders = true,
                ValidateProvidersByYearRange = 2                
            };

            DatasetVersion existingDatasetVersion = new DatasetVersion()
            {
                Description = "description",
                Version = 1,
            };

            Dataset existingDataset = new Dataset()
            {
                Id = model.DatasetId,
                Current = existingDatasetVersion,
                Definition = new DatasetDefinitionVersion { Id = datasetDefinition.Id, Name = datasetDefinition.Name },
                Name = "name",
            };

            IEnumerable<DatasetDefinition> datasetDefinitions = new[]
            {
                datasetDefinition
            };

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository
                .GetRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(new[] { "spec-1" }));

            datasetRepository
                .GetDatasetsByQuery(Arg.Any<Expression<Func<DocumentEntity<Dataset>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<Dataset>>(new[] { existingDataset }));

            List<DatasetValidationError> errors = new List<DatasetValidationError>
            {
                new DatasetValidationError { ErrorMessage = "error" }
            };

            ValidationResult validationResult = new ValidationResult(new[]{
                new ValidationFailure("prop1", "any error")
            });

            datasetUploadValidator = CreateDatasetUploadValidator(validationResult);

            IEnumerable<TableLoadResult> tableLoadResults = new[]
            {
                new TableLoadResult{ GlobalErrors = errors }
            };

            ApiResponse<ProviderVersion> providerVersionResponse = new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
            {
                Providers = new[]
                {
                    new Common.ApiClient.Providers.Models.Provider()
                },
                TargetDate = DateTimeOffset.Now
            });

            IProvidersApiClient providersApiClient = CreateProvidersApiClient();
            providersApiClient
                .GetCurrentProvidersForFundingStream(FundingStreamId)
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.BadRequest));
            providersApiClient
                .GetProvidersByVersion(Arg.Any<string>())
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
                {
                    Providers = new[] { new Common.ApiClient.Providers.Models.Provider() },
                    TargetDate = DateTimeOffset.Now
                }));

            ISpecificationsApiClient specificationsApiClient = CreateSpecificationsApiClient();
            specificationsApiClient
                .GetDistinctProviderVersionIdsFromSpecifications(Arg.Any<IEnumerable<string>>())
                .Returns(new ApiResponse<IEnumerable<string>>(HttpStatusCode.OK, new[] { "providerVersion1" }));

            IMapper mapper = CreateMapper();

            DatasetService service = CreateDatasetService(
                logger: logger,
                blobClient: blobClient,
                datasetRepository: datasetRepository,
                datasetUploadValidator: datasetUploadValidator,
                providersApiClient: providersApiClient,
                mapper: mapper,
                policyRepository: policyRepository,
                specificationsApiClient: specificationsApiClient);

            // Act
            Func<Task> result = () => service.Run(message);

            // Assert
            var exception = await result.Should().ThrowAsync<RetriableException>();
            exception.Which.Message.Should().Be(errorMessage);
        }

        [TestMethod]
        public async Task OnValidateDataset_GivenSpecificationsFetchFailed_ThrowsNonRetriableException()
        {
            //Arrange
            string errorMessage = $"Failed validation - No specifications to get providers for funding stream {FundingStreamId};";

            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx"));

            string uploadedBlobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";
            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.v{model.Version}.xlsx";

            ServiceBusReceivedMessage message = GetValidateDatasetMessage(model);

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId },
                { "fundingStreamId", FundingStreamId },
                { "name", "test-dataset-name" }
            };

            IPolicyRepository policyRepository = CreatePolicyRepository();
            policyRepository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .CopyBlobAsync(Arg.Is(uploadedBlobPath), Arg.Is(blobPath))
                .Returns(new BlockBlobClient(blob.Uri));
            blobClient
                .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
                .Returns(memoryStream);

            // Set up GetBlobPropertiesAsync to return properties with metadata
            var blobProperties = BlobsModelFactory.BlobProperties(metadata: metaData);
            var response = Response.FromValue(blobProperties, null);
            blobClient
                .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
                .Returns(response);

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = DataDefinitionId,
                FundingStreamId = FundingStreamId,
                ValidateProviders = true,
                ValidateProvidersByYearRange = 2
            };

            IEnumerable<DatasetDefinition> datasetDefinitions = new[]
            {
                datasetDefinition
            };

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository.GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Any<string>())
                .Returns(new List<string>());

            List<DatasetValidationError> errors = new List<DatasetValidationError>
            {
                new DatasetValidationError { ErrorMessage = "error" }
            };

            ValidationResult validationResult = new ValidationResult(new[]{
                new ValidationFailure("prop1", "any error")
            });

            datasetUploadValidator = CreateDatasetUploadValidator(validationResult);

            IEnumerable<TableLoadResult> tableLoadResults = new[]
            {
                new TableLoadResult{ GlobalErrors = errors }
            };

            ApiResponse<ProviderVersion> providerVersionResponse = new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
            {
                Providers = new[]
                {
                    new Common.ApiClient.Providers.Models.Provider()
                },
                TargetDate = DateTimeOffset.Now
            });

            IProvidersApiClient providersApiClient = CreateProvidersApiClient();
            providersApiClient
                .GetCurrentProvidersForFundingStream(FundingStreamId)
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
                {
                    Providers = new[]
                    {
                        new Common.ApiClient.Providers.Models.Provider()
                    },
                    TargetDate = DateTimeOffset.Now
                }));

            IMapper mapper = CreateMapper();

            DatasetService service = CreateDatasetService(
                logger: logger,
                blobClient: blobClient,
                datasetRepository: datasetRepository,
                datasetUploadValidator: datasetUploadValidator,
                mapper: mapper,
                policyRepository: policyRepository,
                providersApiClient: providersApiClient);

            // Act
            Func<Task> result = () => service.Run(message);

            // Assert
            result
               .Should()
               .ThrowExactly<NonRetriableException>()
               .Which
               .Message
               .Should()
               .Be(errorMessage);
        }

        [TestMethod]
        public async Task OnValidateDataset_GivenSpecificationApiFailed_ThrowsRetriableException()
        {
            //Arrange
            string errorMessage = $"Failed to fetch specification provider version Ids to get providers for funding stream {FundingStreamId} with status code: BadRequest";

            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx")
                .WithVersion(2));


            string uploadedBlobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";
            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.v{model.Version}.xlsx";

            ServiceBusReceivedMessage message = GetValidateDatasetMessage(model);

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId },
                { "fundingStreamId", FundingStreamId },
                { "name", "test-dataset-name" }
            };

            IPolicyRepository policyRepository = CreatePolicyRepository();
            policyRepository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .CopyBlobAsync(Arg.Is(uploadedBlobPath), Arg.Is(blobPath))
                .Returns(new BlockBlobClient(blob.Uri));
            blobClient
                .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
                .Returns(memoryStream);
            blobClient
                .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
                .Returns(Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: metaData), null)));

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = DataDefinitionId,
                FundingStreamId = FundingStreamId,
                ValidateProviders = true,
                ValidateProvidersByYearRange = 2                
            };

            DatasetVersion existingDatasetVersion = new DatasetVersion()
            {
                Description = "description",
                Version = 1,
            };

            Dataset existingDataset = new Dataset()
            {
                Id = model.DatasetId,
                Current = existingDatasetVersion,
                Definition = new DatasetDefinitionVersion { Id = datasetDefinition.Id, Name = datasetDefinition.Name },
                Name = "name",
            };

            IEnumerable<DatasetDefinition> datasetDefinitions = new[]
            {
                datasetDefinition
            };

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository
                .GetRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(new[] { "spec-1" }));

            datasetRepository
                .GetDatasetsByQuery(Arg.Any<Expression<Func<DocumentEntity<Dataset>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<Dataset>>(new[] { existingDataset }));

            List<DatasetValidationError> errors = new List<DatasetValidationError>
            {
                new DatasetValidationError { ErrorMessage = "error" }
            };

            ValidationResult validationResult = new ValidationResult(new[]{
                new ValidationFailure("prop1", "any error")
            });

            datasetUploadValidator = CreateDatasetUploadValidator(validationResult);

            IEnumerable<TableLoadResult> tableLoadResults = new[]
            {
                new TableLoadResult{ GlobalErrors = errors }
            };

            ApiResponse<ProviderVersion> providerVersionResponse = new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
            {
                Providers = new[]
                {
                    new Common.ApiClient.Providers.Models.Provider()
                },
                TargetDate = DateTimeOffset.Now
            });

            IProvidersApiClient providersApiClient = CreateProvidersApiClient();
            providersApiClient
                .GetCurrentProvidersForFundingStream(FundingStreamId)
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
                {
                    Providers = new[]
                    {
                        new Common.ApiClient.Providers.Models.Provider()
                    },
                    TargetDate = DateTimeOffset.Now
                }));
            providersApiClient
                .GetProvidersByVersion(Arg.Any<string>())
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.BadRequest, new ProviderVersion()));

            ISpecificationsApiClient specificationsApiClient = CreateSpecificationsApiClient();
            specificationsApiClient
                .GetDistinctProviderVersionIdsFromSpecifications(Arg.Any<IEnumerable<string>>())
                .Returns(new ApiResponse<IEnumerable<string>>(HttpStatusCode.BadRequest, (IEnumerable<string>)null));

            IMapper mapper = CreateMapper();

            DatasetService service = CreateDatasetService(
                logger: logger,
                blobClient: blobClient,
                datasetRepository: datasetRepository,
                datasetUploadValidator: datasetUploadValidator,
                providersApiClient: providersApiClient,
                mapper: mapper,
                policyRepository: policyRepository,
                specificationsApiClient: specificationsApiClient);

            // Act
            Func<Task> result = () => service.Run(message);

            // Assert
            result
               .Should()
               .ThrowExactly<RetriableException>()
               .Which
               .Message
               .Should()
               .Be(errorMessage);
        }

        [TestMethod]
        public async Task OnValidateDataset_GivenProvidersApiToGetProvidersByVersionIdFailed_ThrowsRetriableException()
        {
            //Arrange
            string errorMessage = $"Failed to fetch providers for funding stream {FundingStreamId} with status code: BadRequest";

            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx")
                .WithVersion(2));

            string uploadedBlobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";
            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.v{model.Version}.xlsx";

            ServiceBusReceivedMessage message = GetValidateDatasetMessage(model);

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId },
                { "fundingStreamId", FundingStreamId },
                { "name", "test-dataset-name" }
            };

            IPolicyRepository policyRepository = CreatePolicyRepository();
            policyRepository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .CopyBlobAsync(Arg.Is(uploadedBlobPath), Arg.Is(blobPath))
                .Returns(new BlockBlobClient(blob.Uri));
            blobClient
                .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
                .Returns(memoryStream);
            
            // Set up GetBlobPropertiesAsync to return properties with metadata
            var blobProperties = BlobsModelFactory.BlobProperties(metadata: metaData);
            var response = Response.FromValue(blobProperties, null);
            blobClient
                .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
                .Returns(response);

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = DataDefinitionId,
                FundingStreamId = FundingStreamId,
                ValidateProviders = true,
                ValidateProvidersByYearRange = 2,
                TableDefinitions = new List<TableDefinition>
                {
                    new TableDefinition
                    {
                        FieldDefinitions = new List<FieldDefinition>
                        {
                            new FieldDefinition { IdentifierFieldType = IdentifierFieldType.UKPRN }
                        }
                    }
                }
            };

            DatasetVersion existingDatasetVersion = new DatasetVersion()
            {
                Description = "description",
                Version = 1,
            };

            Dataset existingDataset = new Dataset()
            {
                Id = model.DatasetId,
                Current = existingDatasetVersion,
                Definition = new DatasetDefinitionVersion { Id = datasetDefinition.Id, Name = datasetDefinition.Name },
                Name = "name",
            };

            IEnumerable<DatasetDefinition> datasetDefinitions = new[]
            {
                datasetDefinition
            };

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository
                .GetRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(new[] { "spec-1" }));

            datasetRepository
                .GetDatasetsByQuery(Arg.Any<Expression<Func<DocumentEntity<Dataset>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<Dataset>>(new[] { existingDataset }));

            List<DatasetValidationError> errors = new List<DatasetValidationError>
            {
                new DatasetValidationError { ErrorMessage = "error" }
            };

            ValidationResult validationResult = new ValidationResult(new[]{
                new ValidationFailure("prop1", "any error")
            });

            datasetUploadValidator = CreateDatasetUploadValidator(validationResult);

            IEnumerable<TableLoadResult> tableLoadResults = new[]
            {
                new TableLoadResult{ GlobalErrors = errors }
            };

            ApiResponse<ProviderVersion> providerVersionResponse = new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
            {
                Providers = new[]
                {
                    new Common.ApiClient.Providers.Models.Provider()
                },
                TargetDate = DateTimeOffset.Now
            });

            IProvidersApiClient providersApiClient = CreateProvidersApiClient();
            providersApiClient
                .GetCurrentProvidersForFundingStream(FundingStreamId)
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
                {
                    Providers = new[]
                    {
                        new Common.ApiClient.Providers.Models.Provider()
                    },
                    TargetDate = DateTimeOffset.Now
                }));
            providersApiClient
                .GetProvidersByVersion(Arg.Any<string>())
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.BadRequest, new ProviderVersion()));

            ISpecificationsApiClient specificationsApiClient = CreateSpecificationsApiClient();
            specificationsApiClient
                .GetDistinctProviderVersionIdsFromSpecifications(Arg.Any<IEnumerable<string>>())
                .Returns(new ApiResponse<IEnumerable<string>>(HttpStatusCode.OK, new[] { "providerVersion1" }));

            IMapper mapper = CreateMapper();

            DatasetService service = CreateDatasetService(
                logger: logger,
                blobClient: blobClient,
                datasetRepository: datasetRepository,
                datasetUploadValidator: datasetUploadValidator,
                providersApiClient: providersApiClient,
                mapper: mapper,
                policyRepository: policyRepository,
                specificationsApiClient: specificationsApiClient);

            // Act
            Func<Task> result = () => service.Run(message);

            // Assert
            result
               .Should()
               .ThrowExactly<RetriableException>()
               .Which
               .Message
               .Should()
               .Be(errorMessage);
        }

        [TestMethod]
        public async Task OnValidateDataset_GivenSpecificationsFetchFailed_ThrowsNonRetriableExceptions()
        {
            //Arrange
            string errorMessage = $"Failed validation - No specifications to get providers for funding stream {FundingStreamId};";

            GetDatasetBlobModel model = NewGetDatasetBlobModel(_ => _
                .WithDefinitionId(DataDefinitionId)
                .WithDatasetId(DatasetId)
                .WithFileName("blah.xlsx"));

            string uploadedBlobPath = $"{model.DatasetId}/v{model.Version}/blah.uploaded.xlsx";
            string blobPath = $"{model.DatasetId}/v{model.Version}/blah.v{model.Version}.xlsx";

            ServiceBusReceivedMessage message = GetValidateDatasetMessage(model);

            ILogger logger = CreateLogger();

            IDictionary<string, string> metaData = new Dictionary<string, string>
            {
                { "dataDefinitionId", DataDefinitionId },
                { "fundingStreamId", FundingStreamId },
                { "name", "test-dataset-name" }
            };

            IPolicyRepository policyRepository = CreatePolicyRepository();
            policyRepository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            BlobClient blob = new BlobClient(new Uri($"http://localhost/{blobPath}"));

            MemoryStream memoryStream = new MemoryStream(CreateTestExcelPackage());

            IBlobClient blobClient = CreateBlobClient();
            blobClient
                .CopyBlobAsync(Arg.Is(uploadedBlobPath), Arg.Is(blobPath))
                .Returns(new BlockBlobClient(blob.Uri));
            blobClient
                .DownloadToStreamAsync(Arg.Any<BlobBaseClient>())
                .Returns(memoryStream);

            // Set up GetBlobPropertiesAsync to return properties with metadata
            var blobProperties = BlobsModelFactory.BlobProperties(metadata: metaData);
            var response = Response.FromValue(blobProperties, null);
            blobClient
                .GetBlobPropertiesAsync(Arg.Any<BlobBaseClient>())
                .Returns(response);

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = DataDefinitionId,
                FundingStreamId = FundingStreamId,
                ValidateProviders = true,
                ValidateProvidersByYearRange = 2
            };

            IEnumerable<DatasetDefinition> datasetDefinitions = new[]
            {
                datasetDefinition
            };

            IDatasetRepository datasetRepository = CreateDatasetsRepository();
            datasetRepository
                .GetDatasetDefinitionsByQuery(Arg.Any<Expression<Func<DocumentEntity<DatasetDefinition>, bool>>>())
                .Returns(Task.FromResult<IEnumerable<DatasetDefinition>>(datasetDefinitions));
            datasetRepository
                .GetRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Any<string>())
                .Returns(Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

            List<DatasetValidationError> errors = new List<DatasetValidationError>
            {
                new DatasetValidationError { ErrorMessage = "error" }
            };

            ValidationResult validationResult = new ValidationResult(new[]{
                new ValidationFailure("prop1", "any error")
            });

            datasetUploadValidator = CreateDatasetUploadValidator(validationResult);

            IEnumerable<TableLoadResult> tableLoadResults = new[]
            {
                new TableLoadResult{ GlobalErrors = errors }
            };

            IProvidersApiClient providersApiClient = CreateProvidersApiClient();
            providersApiClient
                .GetCurrentProvidersForFundingStream(FundingStreamId)
                .Returns(new ApiResponse<ProviderVersion>(HttpStatusCode.OK, new ProviderVersion
                {
                    Providers = new[]
                    {
                        new Common.ApiClient.Providers.Models.Provider()
                    },
                    TargetDate = DateTimeOffset.Now
                }));

            IMapper mapper = CreateMapper();

            DatasetService service = CreateDatasetService(
                logger: logger,
                blobClient: blobClient,
                datasetRepository: datasetRepository,
                datasetUploadValidator: datasetUploadValidator,
                mapper: mapper,
                policyRepository: policyRepository,
                providersApiClient: providersApiClient);

            // Act
            Func<Task> result = () => service.Run(message);

            // Assert
            result
               .Should()
               .ThrowExactly<NonRetriableException>()
               .Which
               .Message
               .Should()
               .Be(errorMessage);
        }

        // Add this helper method to the test class to fix CS0103
        private static GetDatasetBlobModel NewGetDatasetBlobModel(Action<GetDatasetBlobModelBuilder> setProperties)
        {
            var builder = new GetDatasetBlobModelBuilder();
            setProperties?.Invoke(builder);
            return builder.Build();
        }

        // Add this builder class to the test class or as a nested private class
        private class GetDatasetBlobModelBuilder
        {
            private readonly GetDatasetBlobModel _model = new GetDatasetBlobModel();

            public GetDatasetBlobModelBuilder WithDefinitionId(string definitionId)
            {
                _model.DefinitionId = definitionId;
                return this;
            }

            public GetDatasetBlobModelBuilder WithDatasetId(string datasetId)
            {
                _model.DatasetId = datasetId;
                return this;
            }

            public GetDatasetBlobModelBuilder WithFileName(string fileName)
            {
                _model.Filename = fileName;
                return this;
            }

            public GetDatasetBlobModelBuilder WithVersion(int version)
            {
                _model.Version = version;
                return this;
            }

            public GetDatasetBlobModel Build()
            {
                return _model;
            }
        }

        // Add this helper method to the test class to fix CS0103
        private static ServiceBusReceivedMessage GetValidateDatasetMessage(GetDatasetBlobModel model)
        {
            var messageProperties = new Dictionary<string, object>
            {
                ["operation-id"] = Guid.NewGuid().ToString()
            };
            string serializedModel = Newtonsoft.Json.JsonConvert.SerializeObject(model);
            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: new BinaryData(serializedModel),
                properties: messageProperties,
                messageId: Guid.NewGuid().ToString());
            return message;
        }
    }
}
