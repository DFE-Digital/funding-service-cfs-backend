using AutoMapper;
using CalculateFunding.Common.ApiClient.Providers;
using CalculateFunding.Common.Config.ApiClient.Calcs;
using CalculateFunding.Common.Config.ApiClient.Jobs;
using CalculateFunding.Common.Config.ApiClient.Policies;
using CalculateFunding.Common.Config.ApiClient.Providers;
using CalculateFunding.Common.Config.ApiClient.Specifications;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Functions.Datasets.ServiceBus;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Interfaces.Helpers;
using CalculateFunding.Services.Core.Interfaces.Services;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Core.Services;
using CalculateFunding.Services.DataImporter;
using CalculateFunding.Services.DataImporter.Validators;
using CalculateFunding.Services.DataImporter.Validators.Models;
using CalculateFunding.Services.Datasets;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Datasets.MappingProfiles;
using CalculateFunding.Services.Datasets.Validators;
using CalculateFunding.Services.DeadletterProcessor;
using CalculateFunding.Services.Processing.Interfaces;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using Polly.Bulkhead;
using ServiceCollectionExtensions = CalculateFunding.Services.Core.Extensions.ServiceCollectionExtensions;
using BlobClient = CalculateFunding.Common.Storage.BlobClient;
using IBlobClient = CalculateFunding.Common.Storage.IBlobClient;
using LocalBlobClient = CalculateFunding.Services.Core.AzureStorage.BlobClient;
using LocalIBlobClient = CalculateFunding.Services.Core.Interfaces.AzureStorage.IBlobClient;
using CalculateFunding.Common.Storage;
using CalculateFunding.Models.Datasets.Converter;
using CalculateFunding.Services.Datasets.Converter;
using CalculateFunding.Services.Core.Caching.FileSystem;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type.Interfaces;
using CalculateFunding.Common.Config.ApiClient.Graph;
using CalculateFunding.Services.Datasets.Excel;
using CalculateFunding.Common.Config.ApiClient.FDS;
using CalculateFunding.Common.ApiClient.FDS;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Sql.Interfaces;
using CalculateFunding.Common.Sql;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using DatasetVersion = CalculateFunding.Models.Datasets.DatasetVersion;
using DatasetDefinition = CalculateFunding.Models.Datasets.Schema.DatasetDefinition;
using ProviderSourceDatasetVersion = CalculateFunding.Models.Datasets.ProviderSourceDatasetVersion;

namespace CalculateFunding.Functions.Datasets
{
    public class Startup 
    {
        public static IServiceProvider RegisterComponents(IServiceCollection builder, IConfiguration azureFuncConfig = null)
        {
            IConfigurationRoot config = ConfigHelper.AddConfig(azureFuncConfig);
            return RegisterComponents(builder, config);
        }

        public static IServiceProvider RegisterComponents(IServiceCollection builder, IConfigurationRoot config)
        {
            return Register(builder, config);
        }

        private static IServiceProvider Register(IServiceCollection builder, IConfigurationRoot config)
        {
            builder.AddAppConfiguration();
            builder.AddSingleton<IConfiguration>(config);

            // These registrations of the functions themselves are just for the DebugQueue. Ideally we don't want these registered in production
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                builder.AddScoped<OnDataDefinitionChanges>();
                builder.AddScoped<OnDatasetEvent>();
                builder.AddScoped<OnDatasetValidationEvent>();
                builder.AddScoped<OnDatasetEventFailure>();
                builder.AddScoped<OnDatasetValidationEventFailure>();
                builder.AddScoped<OnMapFdzDatasetsEventFired>();
                builder.AddScoped<OnMapFdzDatasetsEventFiredFailure>();
                builder.AddScoped<OnDeleteDatasets>();
                builder.AddScoped<OnDeleteDatasetsFailure>();
                builder.AddScoped<OnRunConverterDataMerge>();
                builder.AddScoped<OnRunConverterDataMergeFailure>();
                builder.AddScoped<OnCreateSpecificationConverterDatasetsMerge>();
                builder.AddScoped<OnCreateSpecificationConverterDatasetsMergeFailure>();
                builder.AddScoped<OnConverterWizardActivityCsvGeneration>();
                builder.AddScoped<OnConverterWizardActivityCsvGenerationFailure>();
                builder.AddScoped<OnProcessDatasetObsoleteItems>();
                builder.AddScoped<OnProcessDatasetObsoleteItemsFailure>();
            }

            builder.AddScoped<ISpecificationConverterDataMerge, SpecificationConverterDataMerge>();
            builder.AddScoped<IConverterDataMergeService, ConverterDataMergeService>();
            builder.AddScoped<IDatasetCloneBuilderFactory, DatasetCloneBuilderFactory>();
            builder.AddScoped<IConverterDataMergeLogger, ConverterDataMergeLogger>();
            builder.AddScoped<IConverterEligibleProviderService, ConverterEligibleProviderService>();
            builder.AddScoped<IConverterWizardActivityCsvGenerationGeneratorService, ConverterWizardActivityCsvGenerationGeneratorService>();
            builder.AddScoped<IRelationshipDataExcelWriter, RelationshipDataExcelWriter>();
            builder.AddSingleton<IConverterActivityReportRepository>(ctx =>
            {
                AzureStorageSettings storageSettings = new AzureStorageSettings();

                config.Bind("AzureStorageSettings", storageSettings);

                storageSettings.ContainerName = "converterwizardreports";

                return new ConverterActivityReportRepository(new LocalBlobClient(storageSettings),
                    ctx.GetService<IDatasetsResiliencePolicies>());
            });
            builder.AddSingleton<IFileSystemAccess, FileSystemAccess>();
            builder.AddSingleton<IFileSystemCacheSettings, FileSystemCacheSettings>();
            builder.AddSingleton<ICsvUtils, CsvUtils>();
            builder.AddScoped<IConverterWizardActivityToCsvRowsTransformation, ConverterWizardActivityToCsvRowsTransformation>();
            builder.AddScoped<IValidator<ConverterMergeRequest>, ConverterMergeRequestValidation>();
            builder.AddScoped<IDatasetIndexer, DatasetIndexer>();

            builder.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            builder.AddSingleton<IUserProfileProvider, UserProfileProvider>();

            builder
               .AddScoped<IDefinitionsService, DefinitionsService>();

            builder
                .AddSingleton<IProvidersApiClient, ProvidersApiClient>();

            builder
                .AddSingleton<IFDSApiClient, FDSApiClient>();

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder.AddScoped<IProviderSourceDatasetsRepository, ProviderSourceDatasetsRepository>();
            }
            else
            {
                builder.AddSingleton<IProviderSourceDatasetsRepository, CosmosProviderSourceDatasetsRepository>(ctx =>
                new CosmosProviderSourceDatasetsRepository(CreateCosmosDbSettings(config, "providerdatasets")));
            }

            builder
                .AddScoped<IDatasetService, DatasetService>();

            builder
                .AddScoped<IDatasetDataMergeService, DatasetDataMergeService>();

            builder
                .AddSingleton<IJobManagement, JobManagement>();

            builder
                .AddSingleton<IDeadletterService, DeadletterService>();

            builder
                .AddScoped<IProcessDatasetService, ProcessDatasetService>();

            builder
              .AddScoped<IValidator<CreateNewDatasetModel>, CreateNewDatasetModelValidator>();

            builder
                .AddScoped<IValidator<DatasetVersionUpdateModel>, DatasetVersionUpdateModelValidator>();

            builder
              .AddScoped<IValidator<DatasetMetadataModel>, DatasetMetadataModelValidator>();

            builder
                .AddScoped<IValidator<GetDatasetBlobModel>, GetDatasetBlobModelValidator>();

            builder
               .AddScoped<IValidator<CreateDefinitionSpecificationRelationshipModel>, CreateDefinitionSpecificationRelationshipModelValidator>();

            builder
               .AddScoped<IValidator<UpdateDefinitionSpecificationRelationshipModel>, UpdateDefinitionSpecificationRelationshipModelValidator>();

            builder
               .AddScoped<IValidator<ValidateDefinitionSpecificationRelationshipModel>, ValidateDefinitionSpecificationRelationshipModelValidator>();
            builder.AddSingleton<ITypeIdentifierGenerator, VisualBasicTypeIdentifierGenerator>();

            builder
               .AddSingleton<IExcelDatasetWriter, DataDefinitionExcelWriter>();

            builder
                .AddSingleton<IValidator<ExcelPackage>, DatasetWorksheetValidator>();

            builder
                .AddScoped<IValidator<DatasetDefinition>, DatasetDefinitionValidator>();

            builder
                .AddScoped<IDefinitionChangesDetectionService, DefinitionChangesDetectionService>();

            builder
                .AddScoped<IDatasetDefinitionNameChangeProcessor, DatasetDefinitionNameChangeProcessor>();

            builder
                .AddScoped<IValidator<CreateDatasetDefinitionFromTemplateModel>, CreateDatasetDefinitionFromTemplateModelValidator>();

            builder
                .AddScoped<IPolicyRepository, PolicyRepository>();

            builder
                .AddSingleton<IBlobClient, BlobClient>((ctx) =>
                {
                    BlobStorageOptions storageSettings = new BlobStorageOptions();

                    config.Bind("AzureStorageSettings", storageSettings);

                    storageSettings.ContainerName = "datasets";

                    IBlobContainerRepository blobContainerRepository = new BlobContainerRepository(storageSettings);
                    return new BlobClient(blobContainerRepository);
                });

            builder
                .AddSingleton<LocalIBlobClient, LocalBlobClient>((ctx) =>
                {
                    AzureStorageSettings storageSettings = new AzureStorageSettings();

                    config.Bind("AzureStorageSettings", storageSettings);

                    storageSettings.ContainerName = "datasets";

                    return new LocalBlobClient(storageSettings);
                });

            builder.AddDbContext<CfsDbContext>((options) =>
            {
                ISqlSettings sqlSettings = new SqlSettings();
                config.Bind("releaseManagementSql", sqlSettings);
                options.UseSqlServer(sqlSettings.ConnectionString);
            });
            builder.AddTransient<IUnitOfWork, UnitOfWork>(ctx => new UnitOfWork(ctx.GetRequiredService<CfsDbContext>()));

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder.AddScoped<IProviderSourceDatasetsRepository, ProviderSourceDatasetsRepository>();
            }
            else
            {
                builder.AddSingleton<IProviderSourceDatasetsRepository, CosmosProviderSourceDatasetsRepository>(ctx =>
                new CosmosProviderSourceDatasetsRepository(CreateCosmosDbSettings(config, "providerdatasets")));
            }

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder
                 .AddScoped<IDatasetRepository, DataSetsRepository>();

                builder.AddScoped<IVersionRepository<DatasetVersion>, DatasetVersionsRepository<DatasetVersion>>();
            }
            else
            {
                builder.AddSingleton<IDatasetRepository, CosmosDatasetsRepository>(ctx =>
                {
                    return new CosmosDatasetsRepository(CreateCosmosDbSettings(config, "datasets"));
                });

                builder.AddSingleton<IVersionRepository<DatasetVersion>, VersionRepository<DatasetVersion>>(ctx =>
              new VersionRepository<DatasetVersion>(CreateCosmosDbSettings(config, "datasets"), new NewVersionBuilderFactory<DatasetVersion>()));

            }
                
            builder.AddScoped<IDatasetSearchService, DatasetSearchService>();

            builder.AddScoped<IProviderSourceDatasetVersionKeyProvider, ProviderSourceDatasetVersionKeyProvider>();

            builder.AddScoped<IDatasetDefinitionSearchService, DatasetDefinitionSearchService>();

            builder
               .AddScoped<IDefinitionSpecificationRelationshipService, DefinitionSpecificationRelationshipService>();

            builder
               .AddSingleton<IExcelDatasetReader, ExcelDatasetReader>();

            builder
               .AddSingleton<ICalcsRepository, CalcsRepository>();

            builder.AddTransient<IValidator<DatasetUploadValidationModel>, DatasetUploadValidationModelValidator>();

            MapperConfiguration dataSetsConfig = new MapperConfiguration(c =>
            {
                c.AddProfile<DatasetsMappingProfile>();
                c.AddProfile<CalculationsMappingProfile>();
                c.AddProfile<ProviderMappingProfile>();
                c.AddProfile<FDSDatasetsMappingProfile>();
                c.AddProfile<FDSDatasourceDataMappingProfile>();
            });

            builder
                .AddSingleton(dataSetsConfig.CreateMapper());

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder.AddScoped<IVersionRepository<ProviderSourceDatasetVersion>, ProviderSourceDatasetsVersionRepository<ProviderSourceDatasetVersion>>();
            }
            else
            {
                builder.AddSingleton<IVersionRepository<ProviderSourceDatasetVersion>, VersionRepository<ProviderSourceDatasetVersion>>(ctx =>
                new VersionRepository<ProviderSourceDatasetVersion>(CreateCosmosDbSettings(config, "providerdatasets"), new NewVersionBuilderFactory<ProviderSourceDatasetVersion>()));
            }

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder.AddScoped<IDatasetsAggregationsRepository, DatasetsAggregationsRepository>();
            }

            else
            {
                builder.AddSingleton<IDatasetsAggregationsRepository, CosmosDatasetsAggregationsRepository>(ctx =>
                new CosmosDatasetsAggregationsRepository(CreateCosmosDbSettings(config, "datasetaggregations")));
            }

            builder.AddScoped<IUserProfileProvider, UserProfileProvider>();

            builder.AddCalculationsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddSpecificationsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddJobsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddProvidersInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddPoliciesInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddGraphInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddFdsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);

            builder.AddSearch(config);
            builder
                .AddScoped<ISearchRepository<DatasetIndex>, SearchRepository<DatasetIndex>>();
            builder
                .AddScoped<ISearchRepository<DatasetDefinitionIndex>, SearchRepository<DatasetDefinitionIndex>>();
            builder
                .AddScoped<ISearchRepository<DatasetVersionIndex>, SearchRepository<DatasetVersionIndex>>();

            builder.AddServiceBus(config, "datasets");

            builder.AddCaching(config);

            builder.AddApplicationInsightsTelemetryClient(config, "CalculateFunding.Functions.Datasets");
            builder.AddApplicationInsightsServiceName(config, "CalculateFunding.Functions.Datasets");
            builder.AddLogging("CalculateFunding.Functions.Datasets");
            builder.AddTelemetry();

            builder.AddFeatureToggling(config);

            PolicySettings policySettings = ServiceCollectionExtensions.GetPolicySettings(config);

            DatasetsResiliencePolicies resiliencePolicies = CreateResiliencePolicies(policySettings);

            builder.AddSingleton<IDatasetsResiliencePolicies>(resiliencePolicies);

            builder.AddSingleton<IJobManagementResiliencePolicies>((ctx) =>
            {
                AsyncBulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

                return new JobManagementResiliencePolicies()
                {
                    JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
                };

            });

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder.AddScoped<IVersionBulkRepository<ProviderSourceDatasetVersion>, ProviderSourceDatasetsVersionBulkRepository<ProviderSourceDatasetVersion>>();
                builder.AddScoped<IProviderSourceDatasetBulkRepository, ProviderSourceDatasetBulkRepository>();
            }
            else
            {

                builder.AddSingleton<IVersionBulkRepository<ProviderSourceDatasetVersion>, VersionBulkRepository<ProviderSourceDatasetVersion>>((ctx) =>
            {
                CosmosDbSettings settings = new CosmosDbSettings();

                config.Bind("CosmosDbSettings", settings);

                settings.ContainerName = "providerdatasets";

                CosmosRepository cosmos = new CosmosRepository(settings, new CosmosClientOptions
                {
                    AllowBulkExecution = true
                });

                return new VersionBulkRepository<ProviderSourceDatasetVersion>(cosmos, new NewVersionBuilderFactory<ProviderSourceDatasetVersion>());
            });
                builder.AddSingleton<IProviderSourceDatasetBulkRepository, CosmosProviderSourceDatasetBulkRepository>((ctx) =>
                {
                    CosmosDbSettings settings = new CosmosDbSettings();

                    config.Bind("CosmosDbSettings", settings);

                    settings.ContainerName = "providerdatasets";

                    CosmosRepository cosmos = new CosmosRepository(settings, new CosmosClientOptions
                    {
                        AllowBulkExecution = true
                    });

                    return new CosmosProviderSourceDatasetBulkRepository(cosmos);
                });
            }

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder.AddScoped<IVersionRepository<DefinitionSpecificationRelationshipVersion>, DatasetSpecificationRelationshipVersionsRepository<DefinitionSpecificationRelationshipVersion>>();
            }
            else
            {
                builder.AddSingleton<IVersionRepository<DefinitionSpecificationRelationshipVersion>, VersionRepository<DefinitionSpecificationRelationshipVersion>>((ctx) =>
                {
                    CosmosDbSettings settings = new CosmosDbSettings();

                    config.Bind("CosmosDbSettings", settings);

                    settings.ContainerName = "datasets";

                    CosmosRepository cosmosRepository = new CosmosRepository(settings);

                    return new VersionRepository<DefinitionSpecificationRelationshipVersion>(cosmosRepository, new NewVersionBuilderFactory<DefinitionSpecificationRelationshipVersion>());
                });
            }
               
            return builder.BuildServiceProvider();
        }

        private static CosmosRepository CreateCosmosDbSettings(IConfigurationRoot config, string containerName)
        {
            CosmosDbSettings dbSettings = new CosmosDbSettings();

            config.Bind("CosmosDbSettings", dbSettings);

            dbSettings.ContainerName = containerName;

            return new CosmosRepository(dbSettings);
        }

        private static DatasetsResiliencePolicies CreateResiliencePolicies(PolicySettings policySettings)
        {
            AsyncBulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

            return new DatasetsResiliencePolicies
            {
                SpecificationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CacheProviderRepository = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy),
                ProviderResultsRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                ProviderRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                DatasetRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                DatasetSearchService = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                DatasetVersionSearchService = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                DatasetDefinitionSearchRepository = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                BlobClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                ProvidersApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                PoliciesApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CalculationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                RelationshipVersionRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                GraphApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                FDSApiClientPolicy = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
            };
        }
    }
}
