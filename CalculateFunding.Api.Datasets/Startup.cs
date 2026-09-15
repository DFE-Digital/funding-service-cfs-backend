using AutoMapper;
using CalculateFunding.Common.ApiClient.Providers;
using CalculateFunding.Common.Config.ApiClient.Calcs;
using CalculateFunding.Common.Config.ApiClient.Jobs;
using CalculateFunding.Common.Config.ApiClient.Policies;
using CalculateFunding.Common.Config.ApiClient.Providers;
using CalculateFunding.Common.Config.ApiClient.Specifications;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Interfaces;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.WebApi.Extensions;
using CalculateFunding.Common.WebApi.Http;
using CalculateFunding.Common.WebApi.Middleware;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core.AspNet.Extensions;
using CalculateFunding.Services.Core.AspNet.HealthChecks;
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
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeOpenXml;
using Polly.Bulkhead;
using ServiceCollectionExtensions = CalculateFunding.Services.Core.Extensions.ServiceCollectionExtensions;
using BlobClient = CalculateFunding.Common.Storage.BlobClient;
using IBlobClient = CalculateFunding.Common.Storage.IBlobClient;
using LocalBlobClient = CalculateFunding.Services.Core.AzureStorage.BlobClient;
using LocalIBlobClient = CalculateFunding.Services.Core.Interfaces.AzureStorage.IBlobClient;
using CalculateFunding.Common.Storage;
using System;
using CalculateFunding.Models.Datasets.Converter;
using CalculateFunding.Services.Datasets.Converter;
using CalculateFunding.Services.Core.Caching.FileSystem;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type.Interfaces;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type;
using CalculateFunding.Services.Datasets.Excel;
using CalculateFunding.Common.Config.ApiClient.Graph;
using CalculateFunding.Common.Config.ApiClient.FDS;
using System.Threading;
using CalculateFunding.Common.ApiClient.FDS;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Sql.Interfaces;
using CalculateFunding.Common.Sql;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using DatasetVersion = CalculateFunding.Models.Datasets.DatasetVersion;
using DatasetDefinition = CalculateFunding.Models.Datasets.Schema.DatasetDefinition;
using ProviderSourceDatasetVersion = CalculateFunding.Models.Datasets.ProviderSourceDatasetVersion;

namespace CalculateFunding.Api.Datasets
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddControllers()
                .AddNewtonsoftJson();

            RegisterComponents(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            if (Configuration.IsSwaggerEnabled())
            {
                app.ConfigureSwagger(title: "Datasets Microservice API");
            }

            app.MapWhen(
                    context => !context.Request.Path.Value.StartsWith("/swagger"),
                    appBuilder => {
                        appBuilder.UseMiddleware<ApiKeyMiddleware>();
                        appBuilder.UseHealthCheckMiddleware();
                        appBuilder.UseMiddleware<LoggedInUserMiddleware>();
                        appBuilder.UseRouting();
                        appBuilder.UseAuthentication();
                        appBuilder.UseAuthorization();
                        appBuilder.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });
        }

        public void RegisterComponents(IServiceCollection builder)
        {
            builder.AddAppConfiguration();

            builder.AddScoped<ISpecificationConverterDataMerge, SpecificationConverterDataMerge>();
            builder.AddScoped<IConverterDataMergeService, ConverterDataMergeService>();
            builder.AddScoped<IDatasetCloneBuilderFactory, DatasetCloneBuilderFactory>();
            builder.AddScoped<IConverterDataMergeLogger, ConverterDataMergeLogger>();
            builder.AddScoped<IConverterEligibleProviderService, ConverterEligibleProviderService>();
            builder.AddScoped<IConverterWizardActivityCsvGenerationGeneratorService, ConverterWizardActivityCsvGenerationGeneratorService>();
            builder.AddScoped<IConverterWizardActivityToCsvRowsTransformation, ConverterWizardActivityToCsvRowsTransformation>();
            builder.AddSingleton<IConverterActivityReportRepository>(ctx =>
            {
                AzureStorageSettings storageSettings = new AzureStorageSettings();

                Configuration.Bind("AzureStorageSettings", storageSettings);

                storageSettings.ContainerName = "converterwizardreports";

                return new ConverterActivityReportRepository(new LocalBlobClient(storageSettings),
                    ctx.GetService<IDatasetsResiliencePolicies>());
            });
            builder.AddSingleton<IFileSystemAccess, FileSystemAccess>();
            builder.AddSingleton<IFileSystemCacheSettings, FileSystemCacheSettings>();
            builder.AddSingleton<ICsvUtils, CsvUtils>();
            builder.AddScoped<IValidator<ConverterMergeRequest>, ConverterMergeRequestValidation>();
            builder.AddScoped<IDatasetIndexer, DatasetIndexer>();


            builder.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            
            builder.AddSingleton<IUserProfileProvider, UserProfileProvider>();

            builder
                .AddScoped<IHealthChecker, ControllerResolverHealthCheck>();

            builder
                .AddScoped<IDefinitionsService, DefinitionsService>()
                .AddScoped<IHealthChecker, DefinitionsService>();

            builder
                .AddSingleton<IProvidersApiClient, ProvidersApiClient>();
            builder
                .AddSingleton<IFDSApiClient, FDSApiClient>();

            builder
                .AddScoped<IDatasetService, DatasetService>()
                .AddScoped<IHealthChecker, DatasetService>();

            builder
                .AddScoped<IDatasetDataMergeService, DatasetDataMergeService>();

            builder
                .AddSingleton<IJobManagement, JobManagement>();

            builder
                .AddScoped<IProcessDatasetService, ProcessDatasetService>()
                .AddScoped<IHealthChecker, ProcessDatasetService>();

            builder
              .AddScoped<IValidator<CreateNewDatasetModel>, CreateNewDatasetModelValidator>();

            builder
                .AddScoped<IValidator<DatasetVersionUpdateModel>, DatasetVersionUpdateModelValidator>();

            builder
              .AddScoped<IValidator<DatasetMetadataModel>, DatasetMetadataModelValidator>();

            builder
                .AddScoped<IValidator<GetDatasetBlobModel>, GetDatasetBlobModelValidator>();

            builder
                .AddScoped<IValidator<DatasetDefinition>, DatasetDefinitionValidator>();

            builder
               .AddScoped<IValidator<CreateDefinitionSpecificationRelationshipModel>, CreateDefinitionSpecificationRelationshipModelValidator>();

            builder
               .AddScoped<IValidator<UpdateDefinitionSpecificationRelationshipModel>, UpdateDefinitionSpecificationRelationshipModelValidator>();

            builder
               .AddScoped<IValidator<ValidateDefinitionSpecificationRelationshipModel>,ValidateDefinitionSpecificationRelationshipModelValidator>();
            builder.AddSingleton<ITypeIdentifierGenerator, VisualBasicTypeIdentifierGenerator>();

            builder
                .AddSingleton<IExcelDatasetWriter, DataDefinitionExcelWriter>();

            builder
              .AddSingleton<IValidator<ExcelPackage>, DatasetWorksheetValidator>();

            builder
               .AddScoped<IDefinitionChangesDetectionService, DefinitionChangesDetectionService>();

            builder
               .AddScoped<ISpecificationsService, SpecificationsService >();

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

                    Configuration.Bind("AzureStorageSettings", storageSettings);

                    storageSettings.ContainerName = "datasets";

                    IBlobContainerRepository blobContainerRepository = new BlobContainerRepository(storageSettings);
                    return new BlobClient(blobContainerRepository);
                });

            builder
                .AddSingleton<LocalIBlobClient, LocalBlobClient>((ctx) =>
                {
                    AzureStorageSettings storageSettings = new AzureStorageSettings();

                    Configuration.Bind("AzureStorageSettings", storageSettings);

                    storageSettings.ContainerName = "datasets";

                    return new LocalBlobClient(storageSettings);
                });
            bool useSqlDb = Configuration.GetValue<bool>("UseSQLDB");

            if (useSqlDb)
            {
                builder
                 .AddScoped <IProviderSourceDatasetsRepository, ProviderSourceDatasetsRepository> ();
            }
            else
            {

                builder.AddSingleton<IProviderSourceDatasetsRepository, CosmosProviderSourceDatasetsRepository>(ctx =>
                    new CosmosProviderSourceDatasetsRepository(CreateCosmosDbSettings("providerdatasets")));
            }

            if (useSqlDb)
            {
                builder.AddScoped<IDatasetsAggregationsRepository, DatasetsAggregationsRepository>();
            }
            else 
            { 
                builder.AddSingleton<IDatasetsAggregationsRepository, CosmosDatasetsAggregationsRepository>(ctx =>
                    new CosmosDatasetsAggregationsRepository(CreateCosmosDbSettings("datasetaggregations")));
            }

            if (useSqlDb)
            {
                builder
                .AddScoped<IVersionRepository<ProviderSourceDatasetVersion>, ProviderSourceDatasetsVersionRepository<ProviderSourceDatasetVersion>>();
            }
            else
            {

                builder.AddSingleton<IVersionRepository<ProviderSourceDatasetVersion>, VersionRepository<ProviderSourceDatasetVersion>>(ctx =>
                new VersionRepository<ProviderSourceDatasetVersion>(CreateCosmosDbSettings("providerdatasets"), new NewVersionBuilderFactory<ProviderSourceDatasetVersion>()));
            }

            builder.AddDbContext<CfsDbContext>((options) =>
            {
                ISqlSettings sqlSettings = new SqlSettings();
                Configuration.Bind("releaseManagementSql", sqlSettings);
                options.UseSqlServer(sqlSettings.ConnectionString);
            });

            builder.AddTransient<IUnitOfWork, UnitOfWork>(ctx => new UnitOfWork(ctx.GetRequiredService<CfsDbContext>()));

            if (useSqlDb)
            {
                builder
                 .AddScoped<IDatasetRepository, DataSetsRepository>();
            }
            else
            {

                builder.AddSingleton<IDatasetRepository, CosmosDatasetsRepository>(ctx =>
                new CosmosDatasetsRepository(CreateCosmosDbSettings("datasets")));
            }

            if (useSqlDb)
            {
                builder
                 .AddScoped<IVersionRepository<DatasetVersion>, DatasetVersionsRepository<DatasetVersion>>();
            }
            else
            {
                builder.AddSingleton<IVersionRepository<DatasetVersion>, VersionRepository<DatasetVersion>>(ctx =>
                    new VersionRepository<DatasetVersion>(CreateCosmosDbSettings("datasets"), new NewVersionBuilderFactory<DatasetVersion>()));
            }

            builder
                .AddScoped<IDatasetSearchService, DatasetSearchService>()
                .AddScoped<IHealthChecker, DatasetSearchService>();

            builder.AddScoped<IDatasetDefinitionSearchService, DatasetDefinitionSearchService>();

            builder
               .AddScoped<IDefinitionSpecificationRelationshipService, DefinitionSpecificationRelationshipService>()
               .AddScoped<IHealthChecker, DefinitionSpecificationRelationshipService>();

            builder
               .AddSingleton<IExcelDatasetReader, ExcelDatasetReader>();

            builder
               .AddSingleton<ICalcsRepository, CalcsRepository>();

            builder
                .AddSingleton<IReportService, ReportService>();

            builder
                .AddScoped<IProviderSourceDatasetVersionKeyProvider, ProviderSourceDatasetVersionKeyProvider>();

            builder
                .AddSingleton<ICancellationTokenProvider, HttpContextCancellationProvider>();


            MapperConfiguration dataSetsConfig = new MapperConfiguration(c =>
            {
                c.AddProfile<DatasetsMappingProfile>();
                c.AddProfile<CalculationsMappingProfile>();
                c.AddProfile<ProviderMappingProfile>();
                c.AddProfile<FDSDatasetsMappingProfile>();
            });

            builder
                .AddSingleton(dataSetsConfig.CreateMapper());

            builder.AddCalculationsInterServiceClient(Configuration);
            builder.AddJobsInterServiceClient(Configuration);
            builder.AddProvidersInterServiceClient(Configuration);
            builder.AddPoliciesInterServiceClient(Configuration);
            builder.AddGraphInterServiceClient(Configuration);

            builder.AddSearch(Configuration);
            builder
                .AddScoped<ISearchRepository<DatasetIndex>, SearchRepository<DatasetIndex>>();
            builder
                .AddScoped<ISearchRepository<DatasetDefinitionIndex>, SearchRepository<DatasetDefinitionIndex>>();
            builder
                .AddScoped<ISearchRepository<DatasetVersionIndex>, SearchRepository<DatasetVersionIndex>>();
            
            builder
                .AddScoped<IRelationshipDataExcelWriter, RelationshipDataExcelWriter>();

            builder.AddServiceBus(Configuration);

            builder.AddCaching(Configuration);

            builder.AddFeatureToggling(Configuration);

            builder.AddApplicationInsightsTelemetry();
            builder.AddApplicationInsightsTelemetryClient(Configuration, "CalculateFunding.Api.Datasets");
            builder.AddApplicationInsightsServiceName(Configuration, "CalculateFunding.Api.Datasets");
            builder.AddLogging("CalculateFunding.Api.Datasets");
            builder.AddTelemetry();

            builder.AddApiKeyMiddlewareSettings((IConfigurationRoot)Configuration);

            builder.AddHttpContextAccessor();

            PolicySettings policySettings = ServiceCollectionExtensions.GetPolicySettings(Configuration);

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

            builder.AddScoped<IValidator<DatasetUploadValidationModel>, DatasetUploadValidationModelValidator>();
            builder.AddSpecificationsInterServiceClient(Configuration);

            builder.AddHealthCheckMiddleware();

            if (Configuration.IsSwaggerEnabled())
            {
                builder.ConfigureSwaggerServices(title: "Datasets Microservice API");
            }
            if (useSqlDb)
            {
                builder.AddScoped<IVersionBulkRepository<ProviderSourceDatasetVersion>, ProviderSourceDatasetsVersionBulkRepository<ProviderSourceDatasetVersion>>();
                builder.AddScoped<IProviderSourceDatasetBulkRepository, ProviderSourceDatasetBulkRepository>();
            }
            else
            {
                builder.AddSingleton<IVersionBulkRepository<ProviderSourceDatasetVersion>, VersionBulkRepository<ProviderSourceDatasetVersion>>((ctx) =>
                {
                    CosmosDbSettings settings = new CosmosDbSettings();

                    Configuration.Bind("CosmosDbSettings", settings);

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

                    Configuration.Bind("CosmosDbSettings", settings);

                    settings.ContainerName = "providerdatasets";

                    CosmosRepository cosmos = new CosmosRepository(settings, new CosmosClientOptions
                    {
                        AllowBulkExecution = true
                    });

                    return new CosmosProviderSourceDatasetBulkRepository(cosmos);
                });
            }

            if (useSqlDb)
            {
                builder
                 .AddScoped<IVersionRepository<DefinitionSpecificationRelationshipVersion>, 
                 DatasetSpecificationRelationshipVersionsRepository<DefinitionSpecificationRelationshipVersion>>();
            }
            else
            {
                builder.AddSingleton<IVersionRepository<DefinitionSpecificationRelationshipVersion>, VersionRepository<DefinitionSpecificationRelationshipVersion>>((ctx) =>
                {
                    CosmosDbSettings settings = new CosmosDbSettings();

                    Configuration.Bind("CosmosDbSettings", settings);

                    settings.ContainerName = "datasets";

                    CosmosRepository cosmosRepository = new CosmosRepository(settings);

                    return new VersionRepository<DefinitionSpecificationRelationshipVersion>(cosmosRepository, new NewVersionBuilderFactory<DefinitionSpecificationRelationshipVersion>());
                });
            }
            builder.AddFdsInterServiceClient(Configuration, handlerLifetime: Timeout.InfiniteTimeSpan);
        }

        private CosmosRepository CreateCosmosDbSettings(string containerName)
        {
            CosmosDbSettings dbSettings = new CosmosDbSettings();

            Configuration.Bind("CosmosDbSettings", dbSettings);

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
                FDSApiClientPolicy = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
            };
        }
    }
}
