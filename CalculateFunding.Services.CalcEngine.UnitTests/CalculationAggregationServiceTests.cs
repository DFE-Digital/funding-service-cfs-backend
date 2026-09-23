using CalculateFunding.Common.ApiClient.Results;
using CalculateFunding.Common.Caching;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Services.CalcEngine;
using CalculateFunding.Services.CalcEngine.Interfaces;
using CalculateFunding.Services.Core.Caching;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Polly;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Calculator
{
    [TestClass]
    public class CalculationAggregationServiceTests
    {
        private CalculationAggregationService _calculationAggregationService;
        private ICacheProvider _mockCacheProvider;
        private IDatasetAggregationsRepository _mockDatasetAggregationsRepository;
        private ICalculatorResiliencePolicies _mockResiliencePolicies;
        private IResultsApiClient _resultsApiClient;

        [TestInitialize]
        public void Initialize()
        {
            _mockCacheProvider = Substitute.For<ICacheProvider>();
            _mockDatasetAggregationsRepository = Substitute.For<IDatasetAggregationsRepository>();
            _mockResiliencePolicies = new CalculatorResiliencePolicies
            {
                CacheProvider = Policy.NoOpAsync()
            };
            _resultsApiClient = Substitute.For<IResultsApiClient>();

            _calculationAggregationService = new CalculationAggregationService(
                _mockCacheProvider,
                _mockDatasetAggregationsRepository,
                _mockResiliencePolicies,
                _resultsApiClient);
        }

        [TestMethod]
        public async Task GenerateAllocations_GivenCachedAggregateValuesExistAndAggregationsToAggregateInMessageAreInAnyCase_EnsuresAllocationModelCalledWithCachedAggregates()
        {
            const string specificationId = "spec1";

            List<CalculationAggregation> cachedCalculationAggregates = new List<CalculationAggregation>()
            {
                new CalculationAggregation()
                {
                    Values = new List<AggregateValue>()
                    {
                        new AggregateValue() 
                        {
                            Value = 10,
                            AggregatedType = AggregatedType.Average,
                            FieldDefinitionName = "Calculations.A()"
                        },
                        new AggregateValue()
                        {
                            Value = 20,
                            AggregatedType = AggregatedType.Sum,
                            FieldDefinitionName = "Calculations.A()"
                        }
                    }
                },
                new CalculationAggregation()
                {
                    Values = new List<AggregateValue>()
                    {
                        new AggregateValue()
                        {
                            Value = 30,
                            AggregatedType = AggregatedType.Average,
                            FieldDefinitionName = "Calculations.B()"
                        },
                        new AggregateValue()
                        {
                            Value = 40,
                            AggregatedType = AggregatedType.Sum,
                            FieldDefinitionName = "Calculations.B()"
                        }
                    }
                }
            };
            _mockCacheProvider
                .GetAsync<List<CalculationAggregation>>($"{CacheKeys.CalculationAggregations}{specificationId}")
                .Returns(cachedCalculationAggregates);

            BuildAggregationRequest buildAggregationRequest = new BuildAggregationRequest 
            { 
                SpecificationId = specificationId,
                BatchCount = 3,
                CalculationAggregationData = new List<CalculationAggregationData>()
                {
                    new CalculationAggregationData()
                    {
                        AggregateFunctionName = "Calculations.A()",
                        CalculationId = "1"
                    },
                    new CalculationAggregationData()
                    {
                        AggregateFunctionName = "Calculations.B()",
                        CalculationId = "2"
                    }
                }
            };

            IEnumerable<CalculationAggregation> calculationAggregations = 
                await _calculationAggregationService.BuildAggregations(buildAggregationRequest);

            Assert.IsTrue(calculationAggregations.Count() == 2 &&
                calculationAggregations.ElementAt(0).Values.ElementAt(0).Value == 10 &&
                calculationAggregations.ElementAt(0).Values.ElementAt(1).Value == 20 &&
                calculationAggregations.ElementAt(1).Values.ElementAt(0).Value == 30 &&
                calculationAggregations.ElementAt(1).Values.ElementAt(1).Value == 40);

        }
    }
}
