using AutoMapper;
using CalculateFunding.Common.Config.ApiClient.CalcEngine;
using CalculateFunding.Common.Config.ApiClient.Dataset;
using CalculateFunding.Common.Config.ApiClient.Graph;
using CalculateFunding.Common.Config.ApiClient.Jobs;
using CalculateFunding.Common.Config.ApiClient.Policies;
using CalculateFunding.Common.Config.ApiClient.Providers;
using CalculateFunding.Common.Config.ApiClient.Results;
using CalculateFunding.Common.Config.ApiClient.Specifications;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Interfaces;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Storage;
using CalculateFunding.Common.WebApi.Extensions;
using CalculateFunding.Common.WebApi.Http;
using CalculateFunding.Common.WebApi.Middleware;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Calcs.ObsoleteItems;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Calcs;
using CalculateFunding.Services.Calcs.Analysis;
using CalculateFunding.Services.Calcs.Caching;
using CalculateFunding.Services.Calcs.CodeGen;
using CalculateFunding.Services.Calcs.Interfaces;
using CalculateFunding.Services.Calcs.Interfaces.CodeGen;
using CalculateFunding.Services.Calcs.MappingProfiles;
using CalculateFunding.Services.Calcs.Validators;
using CalculateFunding.Services.CodeGeneration.VisualBasic;
using CalculateFunding.Services.CodeMetadataGenerator;
using CalculateFunding.Services.CodeMetadataGenerator.Interfaces;
using CalculateFunding.Services.Compiler;
using CalculateFunding.Services.Compiler.Interfaces;
using CalculateFunding.Services.Compiler.Languages;
using CalculateFunding.Services.Core.AspNet.Extensions;
using CalculateFunding.Services.Core.AspNet.HealthChecks;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Core.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Polly;
using Polly.Bulkhead;
using CalculateFunding.Services.Calcs.Analysis.ObsoleteItems;
using ServiceCollectionExtensions = CalculateFunding.Services.Core.Extensions.ServiceCollectionExtensions;
using CalculateFunding.Common.Config.ApiClient.FDS;
using Microsoft.EntityFrameworkCore;

namespace CalculateFunding.Api.Calcs
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
            services.AddControllers()
                .AddNewtonsoftJson();

            RegisterComponents(services);

            services.AddFeatureManagement();
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
                app.ConfigureSwagger(title: "Calcs Microservice API");
            }

            app.MapWhen(
                    context => !context.Request.Path.Value.StartsWith("/swagger"),
                    appBuilder =>
                    {
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

            builder.AddScoped<IObsoleteReferenceCleanUp, EnumReferenceCleanUp>();
            builder.AddScoped<IObsoleteReferenceCleanUp, FundingLineReferenceCleanUp>();
            builder.AddScoped<IObsoleteReferenceCleanUp, DataFieldReferenceCleanUp>();
            builder.AddScoped<IObsoleteItemCleanup, ObsoleteItemCleanup>();

            builder.AddSingleton<IUniqueIdentifierProvider, UniqueIdentifierProvider>();

            builder.AddSingleton<IFundingLineRoundingSettings, FundingLineRoundingSettings>();

            builder.AddScoped<ICodeContextCache, CodeContextCache>()
                .AddScoped<ICodeContextBuilder, CodeContextBuilder>();

            builder.AddScoped<ICalculationFundingLineQueryService, CalculationFundingLineQueryService>();

            builder.AddSingleton<IUserProfileProvider, UserProfileProvider>();

            builder.AddSingleton(Configuration);
            builder
                .AddScoped<IHealthChecker, ControllerResolverHealthCheck>();

            builder.AddDbContext<Repositories.Common.EFCore.EntityModel.CfsDbContext>((options) =>
            {
                Common.Sql.Interfaces.ISqlSettings sqlSettings = new Common.Sql.SqlSettings();
                Configuration.Bind("releaseManagementSql", sqlSettings);
                options.UseSqlServer(sqlSettings.ConnectionString);
            });

            builder.AddTransient<Common.EfCore.UnitOfWork.IUnitOfWork, Common.EfCore.UnitOfWork.UnitOfWork>(ctx => new Common.EfCore.UnitOfWork.UnitOfWork(ctx.GetRequiredService<Repositories.Common.EFCore.EntityModel.CfsDbContext>()));

            bool useSqlDb = Configuration.GetValue<bool>("UseSQLDB");

            if (useSqlDb)
            {
                builder.AddScoped<ICalculationsRepository, CalculationsRepository>();
            }
            else
            {

                builder
                .AddSingleton<ICalculationsRepository, CosmosCalculationsRepository>((ctx) =>
                {
                    CosmosDbSettings calcsVersioningDbSettings = new CosmosDbSettings();

                    Configuration.Bind("CosmosDbSettings", calcsVersioningDbSettings);

                    calcsVersioningDbSettings.ContainerName = "calcs";

                    CosmosRepository resultsRepostory = new CosmosRepository(calcsVersioningDbSettings);

                    return new CosmosCalculationsRepository(resultsRepostory);
                });
            }
            builder
               .AddScoped<ICalculationService, CalculationService>()
               .AddScoped<ICalculationNameInUseCheck, CalculationNameInUseCheck>()
               .AddScoped<IInstructionAllocationJobCreation, InstructionAllocationJobCreation>()
               .AddScoped<IHealthChecker, CalculationService>()
               .AddScoped<ICreateCalculationService, CreateCalculationService>()
               .AddScoped<IReferencedSpecificationReMapService, ReferencedSpecificationReMapService>();

            builder
                .AddScoped<IObsoleteItemService, ObsoleteItemService>();

            builder
                .AddScoped<IValidator<ObsoleteItem>, ObsoleteItemValidator>();

            builder
                .AddScoped<IQueueReIndexSpecificationCalculationRelationships, QueueReIndexSpecificationCalculationRelationships>();

            builder
                .AddSingleton<ICalculationCodeReferenceUpdate, CalculationCodeReferenceUpdate>();

            builder
                .AddScoped<ICalculationsSearchService, CalculationSearchService>()
                .AddScoped<IHealthChecker, CalculationSearchService>();

            builder
                .AddScoped<IValidator<Calculation>, CalculationModelValidator>();

            builder
               .AddScoped<IValidator<CalculationCreateModel>, CalculationCreateModelValidator>();

            builder
               .AddScoped<IValidator<CalculationEditModel>, CalculationEditModelValidator>();

            builder
                .AddScoped<IPreviewService, PreviewService>()
                .AddScoped<IHealthChecker, PreviewService>();

            builder
               .AddSingleton<ICompilerFactory, CompilerFactory>();

            builder
                .AddSingleton<CSharpCompiler>()
                .AddSingleton<VisualBasicCompiler>()
                .AddSingleton<VisualBasicSourceFileGenerator>();

            builder
              .AddSingleton<ISourceFileGeneratorProvider, SourceFileGeneratorProvider>();

            builder
               .AddScoped<IValidator<PreviewRequest>, PreviewRequestModelValidator>();

            builder
                .AddScoped<IBuildProjectsService, BuildProjectsService>()
                .AddScoped<IHealthChecker, BuildProjectsService>();

            builder
                .AddScoped<IDatasetReferenceService, DatasetReferenceService>();

            if (useSqlDb)
            {
                builder.AddScoped<IBuildProjectsRepository, BuildProjectsRepository>();
            }
            else
            {

                builder
                .AddSingleton<IBuildProjectsRepository, CosmosBuildProjectsRepository>((ctx) =>
                {
                    CosmosDbSettings calcsVersioningDbSettings = new CosmosDbSettings();

                    Configuration.Bind("CosmosDbSettings", calcsVersioningDbSettings);

                    calcsVersioningDbSettings.ContainerName = "calcs";

                    CosmosRepository resultsRepostory = new CosmosRepository(calcsVersioningDbSettings);

                    return new CosmosBuildProjectsRepository(resultsRepostory);
                });
            }

            builder
                .AddSingleton<ICodeMetadataGeneratorService, ReflectionCodeMetadataGenerator>();

            builder.AddScoped<ISourceCodeService, SourceCodeService>();

            builder
                .AddScoped<IDatasetDefinitionFieldChangesProcessor, DatasetDefinitionFieldChangesProcessor>();

            builder.AddScoped<ICalculationEngineRunningChecker, CalculationEngineRunningChecker>();

            builder
                .AddScoped<IApproveAllCalculationsJobAction, ApproveAllCalculationsJobAction>();

            builder.AddSingleton<ISourceFileRepository, SourceFileRepository>((ctx) =>
            {
                BlobStorageOptions blobStorageOptions = new BlobStorageOptions();

                Configuration.Bind("AzureStorageSettings", blobStorageOptions);

                blobStorageOptions.ContainerName = "source";

                IBlobContainerRepository blobContainerRepository = new BlobContainerRepository(blobStorageOptions);
                return new SourceFileRepository(blobContainerRepository);
            });

            if (useSqlDb)
            {
                builder.AddScoped<IVersionRepository<CalculationVersion>, CalculationVersionsRepository<CalculationVersion>>();
            }
            else
            {

                builder
                .AddSingleton<IVersionRepository<CalculationVersion>, VersionRepository<CalculationVersion>>((ctx) =>
            {
                CosmosDbSettings calcsVersioningDbSettings = new CosmosDbSettings();

                Configuration.Bind("CosmosDbSettings", calcsVersioningDbSettings);

                calcsVersioningDbSettings.ContainerName = "calcs";

                CosmosRepository resultsRepostory = new CosmosRepository(calcsVersioningDbSettings);

                return new VersionRepository<CalculationVersion>(resultsRepostory, new NewVersionBuilderFactory<CalculationVersion>());
            });
            }

            builder
                .AddSingleton<ICancellationTokenProvider, HttpContextCancellationProvider>();

            MapperConfiguration calcConfig = new MapperConfiguration(c =>
            {
                c.AddProfile<CalculationsMappingProfile>();
                c.AddProfile<FDSDatasetsMappingProfile>();
            });

            builder
                .AddSingleton(calcConfig.CreateMapper());

            builder.AddSearch(Configuration);
            builder
                .AddScoped<ISearchRepository<CalculationIndex>, SearchRepository<CalculationIndex>>();
            builder
                .AddScoped<ISearchRepository<ProviderCalculationResultsIndex>, SearchRepository<ProviderCalculationResultsIndex>>();

            builder.AddServiceBus(Configuration);

            builder.AddScoped<IJobManagement, JobManagement>();
            builder.AddScoped<ICalculationsFeatureFlag, CalculationsFeatureFlag>();
            builder.AddScoped<IGraphRepository, GraphRepository>();

            builder.AddProvidersInterServiceClient(Configuration);
            builder.AddSpecificationsInterServiceClient(Configuration);
            builder.AddDatasetsInterServiceClient(Configuration);
            builder.AddJobsInterServiceClient(Configuration);
            builder.AddGraphInterServiceClient(Configuration);
            builder.AddPoliciesInterServiceClient(Configuration);
            builder.AddResultsInterServiceClient(Configuration);
            builder.AddCalcEngineInterServiceClient(Configuration);
            builder.AddFdsInterServiceClient(Configuration);

            builder.AddCaching(Configuration);

            builder.AddApplicationInsightsTelemetry();
            builder.AddApplicationInsightsTelemetryClient(Configuration, "CalculateFunding.Api.Calcs");
            builder.AddApplicationInsightsServiceName(Configuration, "CalculateFunding.Api.Calcs");

            builder.AddLogging("CalculateFunding.Api.Calcs");
            builder.AddTelemetry();
            builder.AddEngineSettings(Configuration);

            builder.AddFeatureToggling(Configuration);

            PolicySettings policySettings = ServiceCollectionExtensions.GetPolicySettings(Configuration);
            AsyncBulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

            ResiliencePolicies resiliencePolicies = CreateResiliencePolicies(totalNetworkRequestsPolicy);

            builder.AddSingleton<ICalcsResiliencePolicies>(resiliencePolicies);
            builder.AddSingleton<IJobManagementResiliencePolicies>((ctx) =>
            {
                return new JobManagementResiliencePolicies()
                {
                    JobsApiClient = resiliencePolicies.JobsApiClient,
                };

            });

            builder.AddApiKeyMiddlewareSettings((IConfigurationRoot)Configuration);

            builder.AddHttpContextAccessor();

            builder.AddHealthCheckMiddleware();

            if (Configuration.IsSwaggerEnabled())
            {
                builder.ConfigureSwaggerServices(title: "Calcs Microservice API", version: "v1");
            }
        }

        private static ResiliencePolicies CreateResiliencePolicies(AsyncPolicy totalNetworkRequestsPolicy)
        {
            return new ResiliencePolicies
            {
                CalculationsRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                CalculationsRepositoryNoOCCRetry = CosmosResiliencePolicyHelper.GenerateCosmosPolicyWithNoOCCRetry(totalNetworkRequestsPolicy),
                CalculationsSearchRepository = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                CacheProviderPolicy = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy),
                CalculationsVersionsRepositoryPolicy = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                SpecificationsRepositoryPolicy = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                BuildProjectRepositoryPolicy = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                MessagePolicy = ResiliencePolicyHelpers.GenerateMessagingPolicy(totalNetworkRequestsPolicy),
                JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                ProvidersApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                SourceFilesRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                DatasetsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                PoliciesApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                SpecificationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                GraphApiClientPolicy = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                ResultsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CalcEngineApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                FDSApiClientPolicy = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
            };
        }
    }
}
