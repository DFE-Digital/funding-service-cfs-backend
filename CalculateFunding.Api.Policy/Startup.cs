using System.Threading;
using AutoMapper;
using CalculateFunding.Common.Config.ApiClient.Calcs;
using CalculateFunding.Common.Config.ApiClient.Jobs;
using CalculateFunding.Common.Config.ApiClient.Results;
using CalculateFunding.Common.Config.ApiClient.Specifications;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Storage;
using CalculateFunding.Common.TemplateMetadata;
using CalculateFunding.Common.WebApi.Extensions;
using CalculateFunding.Common.WebApi.Middleware;
using CalculateFunding.Models.Policy;
using CalculateFunding.Models.Policy.FundingPolicy;
using CalculateFunding.Models.Policy.TemplateBuilder;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core.AspNet.Extensions;
using CalculateFunding.Services.Core.AspNet.HealthChecks;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Core.Services;
using CalculateFunding.Services.Policy;
using CalculateFunding.Services.Policy.Interfaces;
using CalculateFunding.Services.Policy.MappingProfiles;
using CalculateFunding.Services.Policy.TemplateBuilder;
using CalculateFunding.Services.Policy.Validators;
using CalculateFunding.Services.Providers.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly.Bulkhead;
using Serilog;
using TemplateMetadataSchema10 = CalculateFunding.Common.TemplateMetadata.Schema10;
using TemplateMetadataSchema11 = CalculateFunding.Common.TemplateMetadata.Schema11;
using TemplateMetadataSchema12 = CalculateFunding.Common.TemplateMetadata.Schema12;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using System;
using Microsoft.Azure.Cosmos;
using CalculateFunding.Common.Sql.Interfaces;
using CalculateFunding.Common.Sql;

namespace CalculateFunding.Api.Policy
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
               .AddNewtonsoftJson(options =>
               {
                   options.SerializerSettings.MaxDepth = null;
               });

            ////Option 1
            //// Build the service provider.
            //var sp = services.BuildServiceProvider();

            //// Create a scope to obtain a reference to the database
            //// context (ApplicationDbContext).
            //using (var scope = sp.CreateScope())
            //{
            //    var scopedServices = scope.ServiceProvider;
            //    var db = scopedServices.GetRequiredService<CfsDbContext>();

            //    // Ensure the database is created.
            //    //db.Database.EnsureCreated();
            //    services.AddTransient<IUnitOfWork, UnitofWork>(x => new UnitofWork(db));

            //};
            RegisterComponents(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            if (Configuration.GetValue<bool>("UseSQLDB"))
            {
                app.UseHeaderOverrideMiddleware();
            }

            if (Configuration.IsSwaggerEnabled())
            {
                app.ConfigureSwagger(title: "Policy Microservice API");
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

            builder
                .AddSpecificationsInterServiceClient(Configuration, handlerLifetime: Timeout.InfiniteTimeSpan)
                .AddCalculationsInterServiceClient(Configuration, handlerLifetime: Timeout.InfiniteTimeSpan)
                .AddResultsInterServiceClient(Configuration, handlerLifetime: Timeout.InfiniteTimeSpan);

            builder.AddDbContext<EntityModel.CfsDbContext>((options) =>
            {
                ISqlSettings sqlSettings = new SqlSettings();
                Configuration.Bind("releaseManagementSql", sqlSettings);               
                    options.UseSqlServer(sqlSettings.ConnectionString);           
            });

            builder.AddTransient<IUnitOfWork, UnitOfWork>(ctx => new UnitOfWork(ctx.GetRequiredService<EntityModel.CfsDbContext>()));

            builder.AddSingleton<IUserProfileProvider, UserProfileProvider>();

            builder.AddScoped<IIoCValidatorFactory, ValidatorFactory>()
                .AddScoped<IValidator<Reference>, AuthorValidator>();
            builder
                .AddSingleton<IHealthChecker, ControllerResolverHealthCheck>();

            builder
                .AddScoped<IFundingStreamService, FundingStreamService>()
                .AddScoped<IHealthChecker, FundingStreamService>()
                .AddScoped<IValidator<FundingStreamSaveModel>, FundingStreamSaveModelValidator>();

            builder
                .AddScoped<IFundingPeriodService, FundingPeriodService>()
                .AddScoped<IHealthChecker, FundingPeriodService>()
                .AddScoped<IFundingPeriodValidator, FundingPeriodValidator>();

            builder
                .AddScoped<IFundingSchemaService, FundingSchemaService>()
                .AddScoped<IHealthChecker, FundingSchemaService>();

            builder
                .AddScoped<IFundingConfigurationService, FundingConfigurationService>()
                .AddScoped<IHealthChecker, FundingConfigurationService>();

            builder
                .AddScoped<IFundingTemplateService, FundingTemplateService>()
                .AddScoped<IHealthChecker, FundingTemplateService>();

            builder
                .AddScoped<IFundingTemplateValidationService, FundingTemplateValidationService>()
                .AddScoped<IHealthChecker, FundingTemplateValidationService>();

            builder
                .AddScoped<IFundingDateService, FundingDateService>()
                .AddScoped<IHealthChecker, FundingDateService>();

            builder
                .AddSingleton<IFundingSchemaRepository, FundingSchemaRepository>((ctx) =>
                {
                    BlobStorageOptions blobStorageOptions = new BlobStorageOptions();

                    Configuration.Bind("AzureStorageSettings", blobStorageOptions);

                    blobStorageOptions.ContainerName = "fundingschemas";

                    IBlobContainerRepository blobContainerRepository = new BlobContainerRepository(blobStorageOptions);
                    return new FundingSchemaRepository(blobContainerRepository);
                });

            builder
               .AddSingleton<IFundingTemplateRepository, FundingTemplateRepository>((ctx) =>
               {
                   BlobStorageOptions blobStorageOptions = new BlobStorageOptions();

                   Configuration.Bind("AzureStorageSettings", blobStorageOptions);

                   blobStorageOptions.ContainerName = "fundingtemplates";

                   IBlobContainerRepository blobContainerRepository = new BlobContainerRepository(blobStorageOptions);
                   return new FundingTemplateRepository(blobContainerRepository);
               });
            if (Configuration.GetValue<bool>("UseSQLDB"))
            {
                builder
                 .AddScoped<IPolicyRepository, SQLPolicyRepository>();
            } else
            {
                builder
                 .AddSingleton<IPolicyRepository, PolicyRepository>((ctx) =>
                 {
                     CosmosDbSettings cosmosDbSettings = new CosmosDbSettings
                     {
                         ContainerName = "policy"
                     };

                     Configuration.Bind("CosmosDbSettings", cosmosDbSettings);

                     CosmosRepository cosmosRepository = new CosmosRepository(cosmosDbSettings);

                     return new PolicyRepository(cosmosRepository);
                 });
            }

            builder.AddSingleton<IPolicyResiliencePolicies>((ctx) =>
            {
                PolicySettings policySettings = ctx.GetService<PolicySettings>();

                AsyncBulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

                Polly.AsyncPolicy redisPolicy = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy);

                return new PolicyResiliencePolicies
                {
                    PolicyRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    CacheProvider = redisPolicy,
                    FundingSchemaRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    FundingTemplateRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    TemplatesSearchRepository = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                    JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    TemplatesRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    SpecificationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    ResultsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    CalculationsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
                };
            });

            builder.AddScoped<IValidator<FundingConfiguration>, SaveFundingConfigurationValidator>();
            builder.AddScoped<IValidator<FundingPeriodsJsonModel>, FundingPeriodJsonModelValidator>();
            builder.AddScoped<IValidator<FundingDate>, SaveFundingDateValidator>();

            builder.AddScoped<IFundingSchemaVersionParseService, FundingSchemaVersionParseService>();

            RegisterTemplateBuilderComponents(builder);

            builder.AddPolicySettings(Configuration);
            builder.AddJobsInterServiceClient(Configuration);

            MapperConfiguration fundingConfMappingConfig = new MapperConfiguration(c =>
            {
                c.AddProfile<FundingConfigurationMappingProfile>();
            });

            builder
                .AddSingleton(fundingConfMappingConfig.CreateMapper());

            builder.AddSearch(Configuration);

            builder.AddScoped<TemplateSearchService>()
                .AddScoped<IHealthChecker, TemplateSearchService>();

            builder
                .AddScoped<ISearchRepository<TemplateIndex>, SearchRepository<TemplateIndex>>();

            builder.AddCaching(Configuration);

            builder.AddApplicationInsightsTelemetry();
            builder.AddApplicationInsightsTelemetryClient(Configuration, "CalculateFunding.Api.Policy");
            builder.AddApplicationInsightsServiceName(Configuration, "CalculateFunding.Api.Policy");
            builder.AddLogging("CalculateFunding.Api.Policy");
            builder.AddTelemetry();

            builder.AddApiKeyMiddlewareSettings((IConfigurationRoot)Configuration);

            builder.AddHttpContextAccessor();
            builder.AddHealthCheckMiddleware();

            if (Configuration.IsSwaggerEnabled())
            {
                builder.ConfigureSwaggerServices(title: "Policy Microservice API");
            }
        }

        public void RegisterTemplateBuilderComponents(IServiceCollection builder)
        {           
            CosmosDbSettings settings = new CosmosDbSettings();
            Configuration.Bind("CosmosDbSettings", settings);
            settings.ContainerName = "templatebuilder";
            CosmosRepository cosmos = new CosmosRepository(settings);
            builder.AddScoped<ITemplateRepository, TemplateRepository>((ctx) =>
            {              
                CosmosRepository calcsCosmosRepostory = (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                    ? new CosmosRepository(settings, new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        RequestTimeout = new TimeSpan(0, 0, 15),
                        AllowBulkExecution = true,
                    })
                    : new CosmosRepository(settings, new CosmosClientOptions()
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
                  cosmos,Configuration,uow, logger);
            });


            builder
                .AddScoped<ITemplateBuilderService, TemplateBuilderService>()
                .AddScoped<IHealthChecker, TemplateBuilderService>()
                .AddScoped<ITemplateVersionRepository, TemplateVersionRepository>(ctx =>
                 {
                     IUnitOfWork uow = ctx.GetService<IUnitOfWork>();
                     ILogger logger = ctx.GetService<ILogger>();
                     return new TemplateVersionRepository(cosmos, new NewVersionBuilderFactory<TemplateVersion>(), Configuration, uow, logger);
                 });
            builder
                //.AddSingleton<ITemplateBuilderService, TemplateBuilderService>()
                .AddSingleton<ITemplateBlobService, TemplateBlobService>()
                //.AddSingleton<IHealthChecker, TemplateBuilderService>()
                .AddScoped<AbstractValidator<TemplateCreateCommand>, TemplateCreateCommandValidator>()
                .AddScoped<AbstractValidator<TemplateCreateAsCloneCommand>, TemplateCreateAsCloneCommandValidator>()
                .AddScoped<AbstractValidator<TemplateFundingLinesUpdateCommand>, TemplateContentUpdateCommandValidator>()
                .AddScoped <AbstractValidator<TemplateDescriptionUpdateCommand>, TemplateDescriptionUpdateCommandValidator>()
                .AddScoped<AbstractValidator<TemplatePublishCommand>, TemplatePublishCommandValidator>()
                .AddScoped<AbstractValidator<Reference>, AuthorValidator>()
                .AddScoped<AbstractValidator<FindTemplateVersionQuery>, FindTemplateVersionQueryValidator>()
                
                //After remove the above cosmos setting code from Template below code need to uncomment
                //.AddSingleton<ITemplateRepository, TemplateRepository>()
                //.AddSingleton<ITemplateVersionRepository, TemplateVersionRepository>()
                //.AddSingleton<ITemplateVersionRepository, TemplateVersionRepository>(ctx =>
                //{
                //    IUnitOfWork uow = ctx.GetService<IUnitOfWork>();
                //    ILogger logger = ctx.GetService<ILogger>();                   
                //    return new TemplateVersionRepository(cosmos, new NewVersionBuilderFactory<TemplateVersion>(), Configuration, uow, logger);
                //})
                .AddSingleton<ITemplateMetadataResolver>(ctx =>
                {
                    TemplateMetadataResolver resolver = new TemplateMetadataResolver();
                    ILogger logger = ctx.GetService<ILogger>();

                    TemplateMetadataSchema10.TemplateMetadataGenerator schema10Generator = new TemplateMetadataSchema10.TemplateMetadataGenerator(logger);
                    resolver.Register("1.0", schema10Generator);

                    TemplateMetadataSchema11.TemplateMetadataGenerator schema11Generator = new TemplateMetadataSchema11.TemplateMetadataGenerator(logger);
                    resolver.Register("1.1", schema11Generator);

                    TemplateMetadataSchema12.TemplateMetadataGenerator schema12Generator = new TemplateMetadataSchema12.TemplateMetadataGenerator(logger);
                    resolver.Register("1.2", schema12Generator);

                    return resolver;
                });
        }
    }
}
