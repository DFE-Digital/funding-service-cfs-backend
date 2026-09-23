using AutoMapper;
using CalculateFunding.Common.Config.ApiClient.Calcs;
using CalculateFunding.Common.Config.ApiClient.Dataset;
using CalculateFunding.Common.Config.ApiClient.Graph;
using CalculateFunding.Common.Config.ApiClient.Jobs;
using CalculateFunding.Common.Config.ApiClient.Policies;
using CalculateFunding.Common.Config.ApiClient.Providers;
using CalculateFunding.Common.Config.ApiClient.Results;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Sql.Interfaces;
using CalculateFunding.Common.Sql;
using CalculateFunding.Functions.Specs.ServiceBus;
using CalculateFunding.Models.Messages;
using CalculateFunding.Models.Specs;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type.Interfaces;
using CalculateFunding.Services.Core.AzureStorage;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.Core.Interfaces.Threading;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Core.Services;
using CalculateFunding.Services.Core.Threading;
using CalculateFunding.Services.DeadletterProcessor;
using CalculateFunding.Services.Processing.Interfaces;
using CalculateFunding.Services.Specs;
using CalculateFunding.Services.Specs.Interfaces;
using CalculateFunding.Services.Specs.MappingProfiles;
using CalculateFunding.Services.Specs.ObsoleteItems;
using CalculateFunding.Services.Specs.Validators;
using CalculateFunding.Services.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.Bulkhead;
using ServiceCollectionExtensions = CalculateFunding.Services.Core.Extensions.ServiceCollectionExtensions;
using SpecificationVersion = CalculateFunding.Models.Specs.SpecificationVersion;

namespace CalculateFunding.Functions.Specs
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

            builder.AddSingleton<ITypeIdentifierGenerator, VisualBasicTypeIdentifierGenerator>();

            builder.AddSingleton<IObsoleteFundingLineAndEnumDetection, ObsoleteFundingLineAndEnumDetection>();
            builder.AddSingleton<IUniqueIdentifierProvider, UniqueIdentifierProvider>();

            builder.AddSingleton<IUserProfileProvider, UserProfileProvider>();
            builder.AddScoped<ISpecificationTemplateVersionChangedHandler, SpecificationTemplateVersionChangedHandler>();

            builder.AddScoped<IQueueCreateSpecificationJobActions, QueueCreateSpecificationJobAction>();
            builder.AddScoped<IQueueEditSpecificationJobActions, QueueEditSpecificationJobActions>();
            builder.AddScoped<IQueueDeleteSpecificationJobActions, QueueDeleteSpecificationJobAction>();

            // These registrations of the functions themselves are just for the DebugQueue. Ideally we don't want these registered in production
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                builder.AddScoped<OnAddRelationshipEvent>();
                builder.AddScoped<OnReIndexSpecification>();
                builder.AddScoped<OnDeleteSpecifications>();
                builder.AddScoped<OnDeleteSpecificationsFailure>();
                builder.AddScoped<OnDetectObsoleteFundingLines>();
                builder.AddScoped<OnDetectObsoleteFundingLinesFailure>();
            }

            builder.AddDbContext<CfsDbContext>((options) =>
            {
                ISqlSettings sqlSettings = new SqlSettings();
                config.Bind("releaseManagementSql", sqlSettings);
                options.UseSqlServer(sqlSettings.ConnectionString);
            });
            builder.AddTransient<IUnitOfWork, UnitOfWork>(ctx => new UnitOfWork(ctx.GetRequiredService<CfsDbContext>()));

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder
                 .AddScoped<ISpecificationsRepository, SpecificationsRepository>();
            }
            else
            {
                builder.AddSingleton<ISpecificationsRepository, CosmosSpecificationsRepository>((ctx) =>
                { 
                    CosmosDbSettings specsVersioningDbSettings = new CosmosDbSettings();

                    config.Bind("CosmosDbSettings", specsVersioningDbSettings);

                    specsVersioningDbSettings.ContainerName = "specs";

                    CosmosRepository resultsRepository = new CosmosRepository(specsVersioningDbSettings);

                    return new CosmosSpecificationsRepository(resultsRepository);
                });
            }

            builder.AddScoped<ISpecificationsService, SpecificationsService>();
            builder.AddScoped<IValidator<SpecificationCreateModel>, SpecificationCreateModelValidator>();
            builder.AddScoped<IValidator<SpecificationEditModel>, SpecificationEditModelValidator>();
            builder.AddScoped<IValidator<AssignDefinitionRelationshipMessage>, AssignDefinitionRelationshipMessageValidator>();
            builder.AddScoped<IValidator<AssignSpecificationProviderVersionModel>, AssignSpecificationProviderVersionModelValidator>();
            builder.AddScoped<ISpecificationsSearchService, SpecificationsSearchService>();
            builder.AddScoped<IResultsRepository, ResultsRepository>();
            builder.AddScoped<ISpecificationIndexer, SpecificationIndexer>();
            builder.AddScoped<IProducerConsumerFactory, ProducerConsumerFactory>();
            builder.AddScoped<ISpecificationIndexingService, SpecificationIndexingService>();
            builder.AddSingleton<IDeadletterService, DeadletterService>();

            builder
                .AddSingleton<IBlobClient, BlobClient>((ctx) =>
                {
                    AzureStorageSettings storageSettings = new AzureStorageSettings();

                    config.Bind("AzureStorageSettings", storageSettings);

                    storageSettings.ContainerName = "providerversions";

                    return new BlobClient(storageSettings);
                });

            if (config.GetValue<bool>("UseSQLDB"))
            {
                builder
                 .AddScoped<IVersionRepository<Models.Specs.SpecificationVersion>, SpecificationVersionsRepository<Models.Specs.SpecificationVersion>>();
            }
            else
            {

                builder.AddSingleton<IVersionRepository<Models.Specs.SpecificationVersion>, VersionRepository<Models.Specs.SpecificationVersion>>((ctx) =>
            {
                CosmosDbSettings specsVersioningDbSettings = new CosmosDbSettings();

                config.Bind("CosmosDbSettings", specsVersioningDbSettings);

                specsVersioningDbSettings.ContainerName = "specs";

                CosmosRepository cosmosRepository = new CosmosRepository(specsVersioningDbSettings);

                return new VersionRepository<Models.Specs.SpecificationVersion>(cosmosRepository, new NewVersionBuilderFactory<SpecificationVersion>());
            });
            }

            builder
                .AddSingleton<IJobManagement, JobManagement>();

            PolicySettings policySettings = ServiceCollectionExtensions.GetPolicySettings(config);

            AsyncBulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

            builder.AddSingleton<ISpecificationsResiliencePolicies>((ctx) => new SpecificationsResiliencePolicies()
            {
                PoliciesApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CalcsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                ProvidersApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                DatasetsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                SpecificationsSearchRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                SpecificationsRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                ResultsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CacheProvider = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy),
                GraphApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
            });

            builder.AddSingleton<IJobManagementResiliencePolicies>((ctx) => new JobManagementResiliencePolicies()
            {
                JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
            });

            MapperConfiguration mappingConfig = new MapperConfiguration(c =>
            {
                c.AddProfile<SpecificationsMappingProfile>();
            });

            builder.AddSingleton(mappingConfig.CreateMapper());

            builder.AddServiceBus(config, "specs");

            builder.AddSearch(config);
            builder
             .AddScoped<ISearchRepository<SpecificationIndex>, SearchRepository<SpecificationIndex>>();

            builder.AddCaching(config);


            builder.AddResultsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddProvidersInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddPoliciesInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddJobsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddCalculationsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddDatasetsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddGraphInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);

            builder.AddPolicySettings(config);

            builder.AddFeatureToggling(config);

            builder.AddApplicationInsightsTelemetryClient(config, "CalculateFunding.Functions.Specs");
            builder.AddApplicationInsightsServiceName(config, "CalculateFunding.Functions.Specs");
            builder.AddLogging("CalculateFunding.Functions.Specs");
            builder.AddTelemetry();

            builder.AddScoped<IUserProfileProvider, UserProfileProvider>();

            return builder.BuildServiceProvider();
        }
    }
}
