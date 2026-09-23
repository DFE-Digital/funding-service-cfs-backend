using CalculateFunding.Common.ApiClient.Results;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Services.CalcEngine.Interfaces;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Options;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.CalcEngine
{
    public class CalculationAggregationService : ICalculationAggregationService
    {
        private readonly ICacheProvider _cacheProvider;
        private readonly IDatasetAggregationsRepository _datasetAggregationsRepository;
        private readonly IResultsApiClient _resultsApiClient;
        private readonly AsyncPolicy _calcResultsApiPolicy;

        public CalculationAggregationService(
            ICacheProvider cacheProvider,
            IDatasetAggregationsRepository datasetAggregationsRepository,
            ICalculatorResiliencePolicies resiliencePolicies,
            IResultsApiClient resultsApiClient)
        {
            Guard.ArgumentNotNull(cacheProvider, nameof(cacheProvider));
            Guard.ArgumentNotNull(datasetAggregationsRepository, nameof(datasetAggregationsRepository));
            Guard.ArgumentNotNull(resiliencePolicies, nameof(resiliencePolicies));
            Guard.ArgumentNotNull(resiliencePolicies.CacheProvider, nameof(resiliencePolicies.CacheProvider));
            Guard.ArgumentNotNull(resultsApiClient, nameof(resultsApiClient));

            _cacheProvider = cacheProvider;
            _datasetAggregationsRepository = datasetAggregationsRepository;
            _resultsApiClient = resultsApiClient;
            _calcResultsApiPolicy = resiliencePolicies.ResultsApiClient;
        }

        public async Task<IEnumerable<CalculationAggregation>> BuildAggregations(BuildAggregationRequest aggreagationRequest)
        {
            IEnumerable<CalculationAggregation> aggregations = Enumerable.Empty<CalculationAggregation>();

            aggregations = await _cacheProvider.GetAsync<List<CalculationAggregation>>(
                $"{ CacheKeys.DatasetAggregationsForSpecification}{aggreagationRequest.SpecificationId}");

            if (DoesNotExistInCache(aggregations))
            {
                aggregations = (await _datasetAggregationsRepository.GetDatasetAggregationsForSpecificationId(aggreagationRequest.SpecificationId)).Select(m => new CalculationAggregation
                {
                    SpecificationId = m.SpecificationId,
                    Values = m.Fields.IsNullOrEmpty() ? Enumerable.Empty<AggregateValue>() : m.Fields.Select(f => new AggregateValue
                    {
                        AggregatedType = f.FieldType,
                        FieldDefinitionName = f.FieldDefinitionName,
                        Value = f.Value
                    })
                });

                await _cacheProvider.SetAsync($"{CacheKeys.DatasetAggregationsForSpecification}{aggreagationRequest.SpecificationId}", aggregations.ToList());
            }

            if (aggreagationRequest.CalculationAggregationData != null && aggreagationRequest.CalculationAggregationData.Any())
            {
                string batchedCacheKey = $"{CacheKeys.CalculationAggregations}{aggreagationRequest.SpecificationId}";

                IEnumerable<CalculationAggregation> cachedCalculationAggregations = await _cacheProvider.GetAsync<List<CalculationAggregation>>(batchedCacheKey);

                if (cachedCalculationAggregations != null)
                {
                    aggregations = aggregations.Concat(cachedCalculationAggregations);
                }
                else
                {

                    IEnumerable<string> aggregateCalculationIds = aggreagationRequest.CalculationAggregationData.Select(_ => _.CalculationId).ToList();
                    var aggregationCalculationResults = await _calcResultsApiPolicy.ExecuteAsync(() => _resultsApiClient.GetAggregateCalculationResults(aggreagationRequest.SpecificationId, aggregateCalculationIds));

                    IEnumerable<CalculationAggregation> calcAggregations = Enumerable.Empty<CalculationAggregation>();

                    foreach (var aggregateValues in aggregationCalculationResults.Content)
                    {
                        string key = aggreagationRequest.CalculationAggregationData.Where(_ => _.CalculationId.Equals(aggregateValues.CalculationId))
                                                .Select(_ => _.AggregateFunctionName + "()").FirstOrDefault();
                        calcAggregations = calcAggregations.Concat(new[]
{
                            new CalculationAggregation
                            {
                                SpecificationId = aggreagationRequest.SpecificationId,
                                Values = new []
                                {
                                    new AggregateValue { FieldDefinitionName = key, AggregatedType = AggregatedType.Sum, Value = aggregateValues.SumValue},
                                    new AggregateValue { FieldDefinitionName = key, AggregatedType = AggregatedType.Min, Value = aggregateValues.MinValue},
                                    new AggregateValue { FieldDefinitionName = key, AggregatedType = AggregatedType.Max, Value = aggregateValues.MaxValue},
                                    new AggregateValue { FieldDefinitionName = key, AggregatedType = AggregatedType.Average, Value = aggregateValues.AvgValue},
                                }
                            }
                        });
                    }
                    aggregations = aggregations.Concat(calcAggregations);

                    await _cacheProvider.SetAsync($"{CacheKeys.CalculationAggregations}{aggreagationRequest.SpecificationId}", calcAggregations.ToList());

                }
            }

            return aggregations;
        }

        private bool DoesNotExistInCache(IEnumerable<CalculationAggregation> aggregations)
        {
            return aggregations == null;
        }
    }
}
