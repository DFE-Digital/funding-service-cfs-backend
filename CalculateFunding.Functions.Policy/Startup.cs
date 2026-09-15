using CalculateFunding.Common.Config.ApiClient.Jobs;
using CalculateFunding.Common.Config.ApiClient.Policies;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Sql;
using CalculateFunding.Common.Sql.Interfaces;
using CalculateFunding.Functions.Policy.ServiceBus;
using CalculateFunding.Models.Policy;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.DeadletterProcessor;
using CalculateFunding.Services.Policy;
using CalculateFunding.Services.Policy.Interfaces;
using CalculateFunding.Services.Policy.TemplateBuilder;
using CalculateFunding.Services.Policy.Validators;
using CalculateFunding.Services.Processing.Interfaces;
using CalculateFunding.Services.Providers.Validators;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Polly.Bulkhead;
using Serilog;
using ServiceCollectionExtensions = CalculateFunding.Services.Core.Extensions.ServiceCollectionExtensions;

namespace CalculateFunding.Functions.Policy
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

            builder.AddFeatureManagement();

            // These registrations of the functions themselves are just for the DebugQueue. Ideally we don't want these registered in production
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                builder.AddScoped<OnReIndexTemplates>();
            }

            PolicySettings policySettings = ServiceCollectionExtensions.GetPolicySettings(config);
            PolicyResiliencePolicies policyResiliencePolicies = CreateResiliencePolicies(policySettings);
          
            builder.AddDbContext<CfsDbContext>((options) =>
            {
                ISqlSettings sqlSettings = new SqlSettings();
                config.Bind("releaseManagementSql", sqlSettings); 
                    options.UseSqlServer(sqlSettings.ConnectionString);
            });

            builder.AddTransient<IUnitOfWork, UnitOfWork>(ctx => new UnitOfWork(ctx.GetRequiredService<CfsDbContext>()));

            builder.AddSingleton<IJobManagementResiliencePolicies>((ctx) =>
            {
                return new JobManagementResiliencePolicies()
                {
                    JobsApiClient = policyResiliencePolicies.JobsApiClient
                };

            });
            builder.AddSingleton<IUserProfileProvider, UserProfileProvider>();

            builder
                .AddSingleton<IFundingStreamService, FundingStreamService>()
                .AddSingleton<IHealthChecker, FundingStreamService>()
                .AddSingleton<IValidator<FundingStreamSaveModel>, FundingStreamSaveModelValidator>();

            builder
                .AddSingleton<IFundingPeriodService, FundingPeriodService>()
                .AddSingleton<IHealthChecker, FundingPeriodService>()
                .AddSingleton<IFundingPeriodValidator, FundingPeriodValidator>();

            builder.AddSingleton<IValidator<FundingPeriodsJsonModel>, FundingPeriodJsonModelValidator>();

            builder.AddSingleton<IFundingSchemaVersionParseService, FundingSchemaVersionParseService>();

            builder.AddSingleton<ICosmosRepository, CosmosRepository>();
            builder.AddSingleton<ITemplatesReIndexerService, TemplatesReIndexerService>();
            builder.AddCaching(config);
            builder.AddSearch(config);
            builder
                .AddScoped<ISearchRepository<TemplateIndex>, SearchRepository<TemplateIndex>>();
            builder.AddSingleton<IPolicyRepository, PolicyRepository>((ctx) =>
            {
                CosmosDbSettings policyDbSettings = new CosmosDbSettings();

                config.Bind("CosmosDbSettings", policyDbSettings);

                policyDbSettings.ContainerName = "policy";

                CosmosRepository policyCosmosRepository = new CosmosRepository(policyDbSettings);

                return new PolicyRepository(policyCosmosRepository);
            });
           
            //builder.AddSingleton<ITemplateRepository, TemplateRepository>();          

            builder.AddScoped<ITemplateRepository, TemplateRepository>((ctx) =>
            {
                CosmosDbSettings policyDbSettings = new CosmosDbSettings();
                config.Bind("CosmosDbSettings", policyDbSettings);
                policyDbSettings.ContainerName = "templatebuilder";
                CosmosRepository cosmos = new CosmosRepository(policyDbSettings);

                CosmosRepository calcsCosmosRepostory = (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                    ? new CosmosRepository(policyDbSettings, new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        RequestTimeout = new TimeSpan(0, 0, 15),
                        AllowBulkExecution = true,
                    })
                    : new CosmosRepository(policyDbSettings, new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        RequestTimeout = new TimeSpan(0, 0, 15),
                        MaxRequestsPerTcpConnection = 8,
                        MaxTcpConnectionsPerEndpoint = 2,
                        AllowBulkExecution = true,
                    });
                IUnitOfWork uow = ctx.GetService<IUnitOfWork>();
                ILogger logger = ctx.GetService<ILogger>();             
                return new TemplateRepository(
                  cosmos, config, uow,logger);
            });

            builder.AddServiceBus(config, "policy");
            builder.AddApplicationInsightsTelemetryClient(config, "CalculateFunding.Functions.Policy");
            builder.AddApplicationInsightsServiceName(config, "CalculateFunding.Functions.Policy");
            builder.AddLogging("CalculateFunding.Functions.Policy", config);
            builder.AddTelemetry();

            builder.AddSingleton<IPolicyResiliencePolicies>(policyResiliencePolicies);
            builder.AddSingleton<IDeadletterService, DeadletterService>();
            builder.AddSingleton<IJobManagement, JobManagement>();
            builder.AddScoped<ITemplatesReIndexerService, TemplatesReIndexerService>();

            builder.AddJobsInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);
            builder.AddPoliciesInterServiceClient(config, handlerLifetime: Timeout.InfiniteTimeSpan);

            return builder.BuildServiceProvider();
        }

        private static PolicyResiliencePolicies CreateResiliencePolicies(PolicySettings policySettings)
        {
            AsyncBulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

            return new PolicyResiliencePolicies
            {
                PolicyRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CacheProvider = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy),
                FundingSchemaRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                FundingTemplateRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                TemplatesSearchRepository = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                TemplatesRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                SpecificationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                ResultsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                CalculationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
            };
        }
    }
}
