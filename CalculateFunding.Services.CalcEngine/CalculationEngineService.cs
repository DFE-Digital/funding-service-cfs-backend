using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.ApiClient.DataSets;
using CalculateFunding.Common.ApiClient.DataSets.Models;
using CalculateFunding.Common.ApiClient.Models;
using CalculateFunding.Common.ApiClient.Results;
using CalculateFunding.Common.ApiClient.Specifications;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.ServiceBus.Interfaces;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.ProviderLegacy;
using CalculateFunding.Services.CalcEngine.Interfaces;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type;
using CalculateFunding.Services.CodeGeneration.VisualBasic.Type.Interfaces;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Processing;
using FluentValidation;
using Polly;
using Serilog;

namespace CalculateFunding.Services.CalcEngine
{
    public class CalculationEngineService : JobProcessingService, ICalculationEngineService
    {
        private readonly ILogger _logger;
        private readonly ICalculationEngine _calculationEngine;
        private readonly ICacheProvider _cacheProvider;
        private readonly IProviderSourceDatasetsRepository _providerSourceDatasetsRepository;
        private readonly ITelemetry _telemetry;
        private readonly IProviderResultsRepository _providerResultsRepository;
        private readonly ICalculationsRepository _calculationsRepository;
        private readonly ISpecificationsApiClient _specificationsApiClient;
        private readonly IDatasetsApiClient _datasetsApiClient;
        private readonly EngineSettings _engineSettings;
        private readonly AsyncPolicy _cacheProviderPolicy;
        private readonly AsyncPolicy _providerSourceDatasetsRepositoryPolicy;
        private readonly AsyncPolicy _providerResultsRepositoryPolicy;
        private readonly AsyncPolicy _calculationsApiClientPolicy;
        private readonly AsyncPolicy _specificationsApiPolicy;
        private readonly AsyncPolicy _datasetsApiPolicy;
        private readonly ICalculationEngineServiceValidator _calculationEngineServiceValidator;
        private readonly IResultsApiClient _resultsApiClient;
        private readonly ICalculationAggregationService _calculationAggregationService;
        private readonly IAssemblyService _assemblyService;
        private readonly ITypeIdentifierGenerator _typeIdentifierGenerator;

        public CalculationEngineService(
            ILogger logger,
            ICalculationEngine calculationEngine,
            ICacheProvider cacheProvider,
            IMessengerService messengerService,
            IProviderSourceDatasetsRepository providerSourceDatasetsRepository,
            ITelemetry telemetry,
            IProviderResultsRepository providerResultsRepository,
            ICalculationsRepository calculationsRepository,
            EngineSettings engineSettings,
            ICalculatorResiliencePolicies resiliencePolicies,
            IJobManagement jobManagement,
            ISpecificationsApiClient specificationsApiClient,
            IDatasetsApiClient datasetsApiClient,
            IResultsApiClient resultsApiClient,
            IValidator<ICalculatorResiliencePolicies> calculatorResiliencePoliciesValidator,
            ICalculationEngineServiceValidator calculationEngineServiceValidator,
            ICalculationAggregationService calculationAggregationService,
            IAssemblyService assemblyService) : base(jobManagement, logger)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(calculationEngine, nameof(calculationEngine));
            Guard.ArgumentNotNull(cacheProvider, nameof(cacheProvider));
            Guard.ArgumentNotNull(messengerService, nameof(messengerService));
            Guard.ArgumentNotNull(providerSourceDatasetsRepository, nameof(providerSourceDatasetsRepository));
            Guard.ArgumentNotNull(telemetry, nameof(telemetry));
            Guard.ArgumentNotNull(providerResultsRepository, nameof(providerResultsRepository));
            Guard.ArgumentNotNull(calculationsRepository, nameof(calculationsRepository));
            Guard.ArgumentNotNull(engineSettings, nameof(engineSettings));
            Guard.ArgumentNotNull(resiliencePolicies?.SpecificationsApiClient, nameof(resiliencePolicies.SpecificationsApiClient));
            Guard.ArgumentNotNull(resiliencePolicies?.DatasetsApiClient, nameof(resiliencePolicies.DatasetsApiClient));
            Guard.ArgumentNotNull(resiliencePolicies?.CacheProvider, nameof(resiliencePolicies.CacheProvider));
            Guard.ArgumentNotNull(resiliencePolicies?.Messenger, nameof(resiliencePolicies.Messenger));
            Guard.ArgumentNotNull(resiliencePolicies?.ProviderSourceDatasetsRepository, nameof(resiliencePolicies.ProviderSourceDatasetsRepository));
            Guard.ArgumentNotNull(resiliencePolicies?.CalculationResultsRepository, nameof(resiliencePolicies.CalculationResultsRepository));
            Guard.ArgumentNotNull(resiliencePolicies?.CalculationsApiClient, nameof(resiliencePolicies.CalculationsApiClient));
            Guard.ArgumentNotNull(resiliencePolicies?.ResultsApiClient, nameof(resiliencePolicies.ResultsApiClient));
            Guard.ArgumentNotNull(specificationsApiClient, nameof(specificationsApiClient));
            Guard.ArgumentNotNull(datasetsApiClient, nameof(datasetsApiClient));
            Guard.ArgumentNotNull(calculatorResiliencePoliciesValidator, nameof(calculatorResiliencePoliciesValidator));
            Guard.ArgumentNotNull(calculationEngineServiceValidator, nameof(calculationEngineServiceValidator));
            Guard.ArgumentNotNull(resultsApiClient, nameof(resultsApiClient));
            Guard.ArgumentNotNull(calculationAggregationService, nameof(calculationAggregationService));
            Guard.ArgumentNotNull(assemblyService, nameof(assemblyService));

            _calculationEngineServiceValidator = calculationEngineServiceValidator;
            _logger = logger;
            _calculationEngine = calculationEngine;
            _cacheProvider = cacheProvider;
            _providerSourceDatasetsRepository = providerSourceDatasetsRepository;
            _telemetry = telemetry;
            _providerResultsRepository = providerResultsRepository;
            _calculationsRepository = calculationsRepository;
            _engineSettings = engineSettings;
            _cacheProviderPolicy = resiliencePolicies.CacheProvider;
            _providerSourceDatasetsRepositoryPolicy = resiliencePolicies.ProviderSourceDatasetsRepository;
            _providerResultsRepositoryPolicy = resiliencePolicies.CalculationResultsRepository;
            _calculationsApiClientPolicy = resiliencePolicies.CalculationsApiClient;
            _specificationsApiClient = specificationsApiClient;
            _specificationsApiPolicy = resiliencePolicies.SpecificationsApiClient;
            _datasetsApiClient = datasetsApiClient;
            _datasetsApiPolicy = resiliencePolicies.DatasetsApiClient;
            _resultsApiClient = resultsApiClient;
            _calculationAggregationService = calculationAggregationService;
            _assemblyService = assemblyService;

            _typeIdentifierGenerator = new VisualBasicTypeIdentifierGenerator();
        }

        public override async Task Process(ServiceBusReceivedMessage message)
        {
            await GenerateAllocations(message);
        }

        private async Task GenerateAllocations(ServiceBusReceivedMessage message)
        {
            Guard.ArgumentNotNull(message, nameof(message));

            _logger.Debug("Validating new allocations message");

            _calculationEngineServiceValidator.ValidateMessage(_logger, message);

            GenerateAllocationMessageProperties messageProperties = GetMessageProperties(message);

            string specificationId = messageProperties.SpecificationId;

            messageProperties.GenerateCalculationAggregationsOnly = Job.JobDefinitionId == JobConstants.DefinitionNames.GenerateCalculationAggregationsJob;

            _logger.Information($"Generating allocations for specification id {specificationId} on server '{Environment.MachineName}'");

            Stopwatch prerequisiteStopwatch = Stopwatch.StartNew();

            Task<(byte[], long)> getAssemblyTask = GetAssemblyForSpecification(specificationId, messageProperties.AssemblyETag);
            Task<(IEnumerable<ProviderSummary>, long)> providerSummaryTask = GetProviderSummaries(messageProperties);
            Task<(IEnumerable<CalculationSummaryModel>, long)> calculationSummaryTask = GetCalculationSummaries(specificationId);
            Task<(SpecificationSummary, long)> specificationSummaryTask = GetSpecificationSummary(specificationId);
            Task<(IEnumerable<DatasetSpecificationRelationshipViewModel>, long)> datasetRelationshipsTask = GetRelationshipsBySpecificationId(specificationId);
            Task<(IEnumerable<CalculationAggregation>, long)> aggregationsTask = BuildAggregations(messageProperties);

            await TaskHelper.WhenAllAndThrow(getAssemblyTask, providerSummaryTask, calculationSummaryTask, specificationSummaryTask, datasetRelationshipsTask, aggregationsTask);

            (byte[] assembly, long assemblyLookupElapsedMilliseconds) = getAssemblyTask.Result;
            (IEnumerable<ProviderSummary> summaries, long providerSummaryLookupElapsedMilliseconds) = providerSummaryTask.Result;
            (IEnumerable<CalculationSummaryModel> calculations, long calculationsLookupStopwatchElapsedMilliseconds) = calculationSummaryTask.Result;
            (SpecificationSummary specificationSummary, long specificationSummaryElapsedMilliseconds) = specificationSummaryTask.Result;
            (IEnumerable<DatasetSpecificationRelationshipViewModel> datasetRelationships, long datasetRelationshipsElapsedMilliseconds) = datasetRelationshipsTask.Result;
            (IEnumerable<CalculationAggregation> aggregations, long aggregationsElapsedMilliseconds) = aggregationsTask.Result;

            prerequisiteStopwatch.Stop();

            int providerBatchSize = _engineSettings.ProviderBatchSize;

            //Filtering the FDS dataset type that has mapped datasource and all other relationship types
            IEnumerable<string> dataRelationshipIds = datasetRelationships.
                Where(_ => (_.RelationshipType != Common.ApiClient.DataSets.Models.DatasetRelationshipType.FDS)
                || (_.DatasetId != null && _.RelationshipType == Common.ApiClient.DataSets.Models.DatasetRelationshipType.FDS)).Select(_ => _.Id).ToList();

            int totalProviderResults = 0;
            bool calculationResultsHaveExceptions = false;

            for (int i = 0; i < summaries.Count(); i += providerBatchSize)
            {
                Stopwatch calcTiming = Stopwatch.StartNew();

                CalculationResultsModel calculationResults = await CalculateResults(specificationId,
                    summaries,
                    calculations,
                    aggregations,
                    dataRelationshipIds,
                    assembly,
                    messageProperties,
                    providerBatchSize,
                    i,
                    messageProperties.IndicativeOpenerProviderStatuses);

                _logger.Information($"Calculating results complete for specification id {specificationId}");

                long saveCosmosElapsedMs = -1;
                long queueSearchWriterElapsedMs = -1;
                long saveRedisElapsedMs = 0;
                long? saveQueueElapsedMs = null;
                int savedProviders = 0;
                int percentageProvidersSaved = 0;

                if (calculationResults.ProviderResults.Any())
                {
                    (long saveCosmosElapsedMs, long queueSerachWriterElapsedMs, long saveRedisElapsedMs, long? saveQueueElapsedMs, int savedProviders) processResultsMetrics =
                        await ProcessProviderResults(calculationResults.ProviderResults, specificationSummary, messageProperties, message);

                    saveCosmosElapsedMs = processResultsMetrics.saveCosmosElapsedMs;
                    queueSearchWriterElapsedMs = processResultsMetrics.queueSerachWriterElapsedMs;
                    saveRedisElapsedMs = processResultsMetrics.saveRedisElapsedMs;
                    saveQueueElapsedMs = processResultsMetrics.saveQueueElapsedMs;
                    savedProviders = processResultsMetrics.savedProviders;

                    totalProviderResults += calculationResults.ProviderResults.Count();
                    percentageProvidersSaved = savedProviders / totalProviderResults * 100;

                    if (calculationResults.ResultsContainExceptions)
                    {
                        _logger.Warning($"Exception(s) executing specification id '{specificationId}:  {calculationResults.ExceptionMessages}");
                        calculationResultsHaveExceptions = true;
                    }
                }

                calcTiming.Stop();

                IDictionary<string, double> metrics = new Dictionary<string, double>()
                {
                    { "calculation-run-providersProcessed", calculationResults.PartitionedSummaries.Count() },
                    { "calculation-run-lookupCalculationDefinitionsMs", calculationsLookupStopwatchElapsedMilliseconds },
                    { "calculation-run-providersResultsFromCache", summaries.Count() },
                    { "calculation-run-partitionSize", messageProperties.PartitionSize },
                    { "calculation-run-saveProviderResultsRedisMs", saveRedisElapsedMs },
                    { "calculation-run-runningCalculationMs",  calculationResults.CalculationRunMs },
                    { "calculation-run-savedProviders",  savedProviders },
                    { "calculation-run-savePercentage ",  percentageProvidersSaved },
                    { "calculation-run-specLookup ",  specificationSummaryElapsedMilliseconds },
                    { "calculation-run-datasetRelationshipsLookup ",  datasetRelationshipsElapsedMilliseconds },
                    { "calculation-run-providerSummaryLookup ",  providerSummaryLookupElapsedMilliseconds },
                    { "calculation-run-providerSourceDatasetsLookupMs ",  calculationResults.ProviderSourceDatasetsLookupMs },
                    { "calculation-run-assemblyLookup ",  assemblyLookupElapsedMilliseconds },
                    { "calculation-run-aggregationsLookup ",  aggregationsElapsedMilliseconds },
                    { "calculation-run-prerequisiteMs ",  prerequisiteStopwatch.ElapsedMilliseconds },
                };

                if (saveQueueElapsedMs.HasValue)
                {
                    metrics.Add("calculation-run-saveProviderResultsServiceBusMs", saveQueueElapsedMs.Value);
                }

                if (saveCosmosElapsedMs > -1)
                {
                    metrics.Add("calculation-run-batchElapsedMilliseconds", calcTiming.ElapsedMilliseconds);
                    metrics.Add("calculation-run-saveProviderResultsCosmosMs", saveCosmosElapsedMs);
                }
                else
                {
                    metrics.Add("calculation-run-for-tests-ms", calcTiming.ElapsedMilliseconds);
                }

                if (queueSearchWriterElapsedMs > 0)
                {
                    metrics.Add("calculation-run-queueSearchWriterMs", queueSearchWriterElapsedMs);

                }

                _telemetry.TrackEvent("CalculationRunProvidersProcessed",
                    new Dictionary<string, string>()
                    {
                    { "specificationId" , specificationId },
                    },
                    metrics
                );
            }

            ItemsProcessed = summaries.Count();
            ItemsFailed = summaries.Count() - totalProviderResults;

            if (calculationResultsHaveExceptions)
            {
                throw new NonRetriableException($"Exceptions were thrown during generation of calculation results for specification '{specificationId}'");
            }
            else
            {
                await CompleteBatch(messageProperties);
            }
        }

        private async Task<(SpecificationSummary, long)> GetSpecificationSummary(string specificationId)
        {
            Stopwatch specsLookupStopwatch = Stopwatch.StartNew();
            ApiResponse<SpecificationSummary> specificationQuery = await _specificationsApiPolicy.ExecuteAsync(() => _specificationsApiClient.GetSpecificationSummaryById(specificationId));
            if (specificationQuery == null || specificationQuery.StatusCode != HttpStatusCode.OK || specificationQuery.Content == null)
            {
                throw new InvalidOperationException("Specification summary is null");
            }

            specsLookupStopwatch.Stop();

            return (specificationQuery.Content, specsLookupStopwatch.ElapsedMilliseconds);
        }

        private async Task<(IEnumerable<DatasetSpecificationRelationshipViewModel>, long)> GetRelationshipsBySpecificationId(string specificationId)
        {
            Stopwatch datasetRelationshipsLookupStopwatch = Stopwatch.StartNew();
            ApiResponse<IEnumerable<DatasetSpecificationRelationshipViewModel>> datasetRelationshipsQuery = await _datasetsApiPolicy.ExecuteAsync(() => _datasetsApiClient.GetRelationshipsBySpecificationId(specificationId));
          
            if (datasetRelationshipsQuery.StatusCode != HttpStatusCode.OK)
            {
                _logger.Error($"{specificationId} for DatasetRelationshipsQuery returned invalid status code of '{datasetRelationshipsQuery.StatusCode}'");
                throw new InvalidOperationException($"DatasetRelationshipsQuery returned invalid status code of '{datasetRelationshipsQuery.StatusCode}'");
            }
            if (datasetRelationshipsQuery?.Content == null || !datasetRelationshipsQuery.Content.Any())
            {
                _logger.Error($"No Dataset relationships are available : {specificationId}");
            }
            datasetRelationshipsLookupStopwatch.Stop();

            return (datasetRelationshipsQuery.Content, datasetRelationshipsLookupStopwatch.ElapsedMilliseconds);
        }

        private async Task<(IEnumerable<CalculationSummaryModel>, long)> GetCalculationSummaries(string specificationId)
        {
            Stopwatch calculationsLookupStopwatch = Stopwatch.StartNew();
            //when temaplate is change V1 to V2 or V3 .in the cache existing template cals details already exist .so will delete cache and add new teamplate cals information 
            await _cacheProviderPolicy.ExecuteAsync(() => _cacheProvider.KeyDeleteAsync<List<CalculationSummaryModel>>($"{CacheKeys.CalculationsSummariesForSpecification}{specificationId}"));
            IEnumerable<CalculationSummaryModel> calculations = await _calculationsApiClientPolicy.ExecuteAsync(() =>
                _calculationsRepository.GetCalculationSummariesForSpecification(specificationId));

            if (calculations == null)
            {
                _logger.Error($"Calculations lookup API returned null for specification id {specificationId}");

                throw new InvalidOperationException("Calculations lookup API returned null");
            }
            calculationsLookupStopwatch.Stop();

            return (calculations, calculationsLookupStopwatch.ElapsedMilliseconds);
        }

        private async Task<(IEnumerable<ProviderSummary>, long)> GetProviderSummaries(GenerateAllocationMessageProperties messageProperties)
        {
            _logger.Information($"processing partition index {messageProperties.PartitionIndex} for batch size {messageProperties.PartitionSize}");

            int start = messageProperties.PartitionIndex;

            int stop = start + messageProperties.PartitionSize - 1;

            Stopwatch providerSummaryLookup = Stopwatch.StartNew();
            IEnumerable<ProviderSummary> summaries = await _cacheProviderPolicy.ExecuteAsync(() => _cacheProvider.ListRangeAsync<ProviderSummary>(messageProperties.ProviderCacheKey, start, stop));
            if (summaries == null)
            {
                throw new InvalidOperationException("Null provider summaries returned");
            }

            if (!summaries.Any())
            {
                throw new InvalidOperationException("No provider summaries returned to process");
            }

            providerSummaryLookup.Stop();

            return (summaries, providerSummaryLookup.ElapsedMilliseconds);
        }

        private async Task<(byte[], long)> GetAssemblyForSpecification(string specificationId, string etag)
        {
            Stopwatch assemblyLookupStopwatch = Stopwatch.StartNew();

            byte[] assembly = await _assemblyService.GetAssemblyForSpecification(specificationId, etag);

            assemblyLookupStopwatch.Stop();

            return (assembly, assemblyLookupStopwatch.ElapsedMilliseconds);
        }

        private async Task<CalculationResultsModel> CalculateResults(string specificationId, IEnumerable<ProviderSummary> summaries,
            IEnumerable<CalculationSummaryModel> calculations,
            IEnumerable<CalculationAggregation> aggregations,
            IEnumerable<string> dataRelationshipIds,
            byte[] assemblyForSpecification,
            GenerateAllocationMessageProperties messageProperties,
            int providerBatchSize,
            int index,
            IEnumerable<string> indicativeOpenerProviderStatuses)
        {
            ConcurrentBag<ProviderResult> providerResults = new ConcurrentBag<ProviderResult>();

            Guard.ArgumentNotNull(summaries, nameof(summaries));

            IEnumerable<ProviderSummary> partitionedSummaries = summaries.Skip(index).Take(providerBatchSize);

            IList<string> providerIdList = partitionedSummaries.Select(m => m.Id).ToList();

            Stopwatch providerSourceDatasetsStopwatch = Stopwatch.StartNew();

            _logger.Information($"Fetching provider sources for specification id {messageProperties.SpecificationId}");

            Dictionary<string, Dictionary<string, ProviderSourceDataset>> providerSourceDatasetResult = await _providerSourceDatasetsRepositoryPolicy.ExecuteAsync(
                () => _providerSourceDatasetsRepository.GetProviderSourceDatasetsByProviderIdsAndRelationshipIds(specificationId, providerIdList, dataRelationshipIds));

            providerSourceDatasetsStopwatch.Stop();

            _logger.Information($"Fetched provider sources found for specification id {messageProperties.SpecificationId}");


            _logger.Information($"Calculating results for specification id {messageProperties.SpecificationId}");
            Stopwatch assemblyLoadStopwatch = Stopwatch.StartNew();
            Assembly assembly = Assembly.Load(assemblyForSpecification);
            assemblyLoadStopwatch.Stop();

            Stopwatch calculationStopwatch = Stopwatch.StartNew();

            List<Task> allTasks = new List<Task>();
            SemaphoreSlim throttler = new SemaphoreSlim(_engineSettings.CalculateProviderResultsDegreeOfParallelism);

            IAllocationModel allocationModel = _calculationEngine.GenerateAllocationModel(assembly);

            foreach (ProviderSummary provider in partitionedSummaries)
            {
                await throttler.WaitAsync();

                allTasks.Add(
                    Task.Run(() =>
                    {
                        try
                        {
                            if (provider == null)
                            {
                                throw new Exception("Provider summary is null");
                            }

                            if (providerSourceDatasetResult.AnyWithNullCheck())
                            {
                                if (!providerSourceDatasetResult.TryGetValue(provider.Id, out Dictionary<string, ProviderSourceDataset> providerDatasets))
                                {
                                    throw new Exception($"Provider source dataset not found for {provider.Id}.");
                                }

                                ProviderResult result = _calculationEngine.CalculateProviderResults(allocationModel, specificationId, calculations, provider, providerDatasets, aggregations, indicativeOpenerProviderStatuses);

                                if (result == null)
                                {
                                    throw new InvalidOperationException("Null result from Calc Engine CalculateProviderResults");
                                }

                                providerResults.Add(result);
                            }
                        }
                        finally
                        {
                            throttler.Release();
                        }

                        return Task.CompletedTask;
                    }));
            }

            await TaskHelper.WhenAllAndThrow(allTasks.ToArray());

            calculationStopwatch.Stop();

            _logger.Information($"Calculating results complete for specification id {messageProperties.SpecificationId} in {calculationStopwatch.ElapsedMilliseconds}ms");

            return new CalculationResultsModel
            {
                ProviderResults = providerResults,
                PartitionedSummaries = partitionedSummaries,
                CalculationRunMs = calculationStopwatch.ElapsedMilliseconds,
                AssemblyLoadMs = assemblyLoadStopwatch.ElapsedMilliseconds,
                ProviderSourceDatasetsLookupMs = providerSourceDatasetsStopwatch.ElapsedMilliseconds,
            };
        }

        private GenerateAllocationMessageProperties GetMessageProperties(ServiceBusReceivedMessage message)
        {
            GenerateAllocationMessageProperties properties = new GenerateAllocationMessageProperties();

            if (message.ApplicationProperties.ContainsKey("jobId"))
            {
                properties.JobId = message.ApplicationProperties["jobId"].ToString();
            }
            else
            {
                _logger.Error("Missing job id for generating allocations");
            }

            string specificationId = message.ApplicationProperties["specification-id"].ToString();

            properties.SpecificationId = specificationId;

            int batchNumber = 0;

            if (message.ApplicationProperties.ContainsKey("batch-number"))
            {
                batchNumber = int.Parse(message.ApplicationProperties["batch-number"].ToString());
            }

            int batchCount = 0;

            if (message.ApplicationProperties.ContainsKey("batch-count"))
            {
                batchCount = int.Parse(message.ApplicationProperties["batch-count"].ToString());
            }

            properties.BatchNumber = batchNumber;

            properties.BatchCount = batchCount;

            properties.ProviderCacheKey = message.ApplicationProperties["provider-cache-key"].ToString();

            properties.SpecificationSummaryCacheKey = message.ApplicationProperties["specification-summary-cache-key"].ToString();

            properties.PartitionIndex = int.Parse(message.ApplicationProperties["provider-summaries-partition-index"].ToString());

            properties.PartitionSize = int.Parse(message.ApplicationProperties["provider-summaries-partition-size"].ToString());

            properties.CalculationsAggregationsBatchCacheKey = $"{CacheKeys.CalculationAggregations}{specificationId}_{batchNumber}";

            properties.CalculationsToAggregate = message.ApplicationProperties.ContainsKey("calculations-to-aggregate") && !string.IsNullOrWhiteSpace(message.ApplicationProperties["calculations-to-aggregate"].ToString()) ? message.ApplicationProperties["calculations-to-aggregate"].ToString().Split(',') : null;

            properties.IndicativeOpenerProviderStatuses = message.ApplicationProperties.ContainsKey("funding-config-indicative-opener-provider-status")
                                                        && !string.IsNullOrWhiteSpace(message.ApplicationProperties["funding-config-indicative-opener-provider-status"].ToString()) ?
                                                        message.ApplicationProperties["funding-config-indicative-opener-provider-status"].ToString().Split(',')
                                                        : new string[0];

            properties.User = message.GetUserDetails();
            properties.CorrelationId = message.GetCorrelationId();

            properties.AssemblyETag = message.GetUserProperty<string>("assembly-etag");

            return properties;
        }

        private async Task<(IEnumerable<CalculationAggregation>, long)> BuildAggregations(GenerateAllocationMessageProperties messageProperties)
        {
            Stopwatch sw = Stopwatch.StartNew();
            List<CalculationAggregationData> calculationAggregationData = new List<CalculationAggregationData>();
            if (messageProperties.CalculationsToAggregate != null)
            {
                foreach (string calcAggregate in messageProperties.CalculationsToAggregate)
                {
                    string[] calcParts = calcAggregate.Split(":");
                    calculationAggregationData.Add(new CalculationAggregationData()
                    {
                        AggregateFunctionName = calcParts[0],
                        CalculationId = calcParts[1]
                    });
                }
            }
            BuildAggregationRequest aggreagationRequest = new BuildAggregationRequest
            {
                BatchCount = messageProperties.BatchCount,
                GenerateCalculationAggregationsOnly = messageProperties.GenerateCalculationAggregationsOnly,
                SpecificationId = messageProperties.SpecificationId,
                CalculationAggregationData = calculationAggregationData
            };

            IEnumerable<CalculationAggregation> aggregations = await _calculationAggregationService.BuildAggregations(aggreagationRequest);

            return (aggregations, sw.ElapsedMilliseconds);
        }

        private async Task CompleteBatch(GenerateAllocationMessageProperties messageProperties)
        {
            Outcome = $"{ItemsSucceeded} provider results were generated successfully from {ItemsProcessed} providers";

            if (messageProperties.GenerateCalculationAggregationsOnly)
            {
                Outcome = $"{ItemsSucceeded} provider result calculation aggregations were generated successfully from {ItemsProcessed} providers";
            }
        }

        private async Task<(long saveCosmosElapsedMs, long queueSerachWriterElapsedMs, long saveRedisElapsedMs, long? saveQueueElapsedMs, int savedProviders)> ProcessProviderResults(
            IEnumerable<ProviderResult> providerResults,
            SpecificationSummary specificationSummary,
            GenerateAllocationMessageProperties messageProperties,
            ServiceBusReceivedMessage message)
        {
            (long saveToCosmosElapsedMs, long saveToSearchElapsedMs, int savedProviders) saveProviderResultsTimings = (-1, -1, -1);

            if (!message.ApplicationProperties.ContainsKey("ignore-save-provider-results"))
            {
                _logger.Information($"Saving results for specification id {messageProperties.SpecificationId}");

                saveProviderResultsTimings = await _providerResultsRepositoryPolicy.ExecuteAsync(() => _providerResultsRepository.SaveProviderResults(providerResults,
                    specificationSummary,
                    messageProperties.PartitionIndex,
                    messageProperties.PartitionSize,
                    messageProperties.User,
                    messageProperties.CorrelationId,
                    messageProperties.JobId));

                _logger.Information($"Saving results completeed for specification id {messageProperties.SpecificationId}");
            }

            Stopwatch saveQueueStopwatch = null;
            Stopwatch saveRedisStopwatch = null;

            return (saveProviderResultsTimings.saveToCosmosElapsedMs,
                saveProviderResultsTimings.saveToSearchElapsedMs,
                saveRedisStopwatch != null ? saveRedisStopwatch.ElapsedMilliseconds : 0,
                saveQueueStopwatch?.ElapsedMilliseconds,
                saveProviderResultsTimings.savedProviders);
        }

        private bool DoesNotExistInCache(IEnumerable<CalculationAggregation> aggregations)
        {
            return aggregations == null;
        }
    }
}
