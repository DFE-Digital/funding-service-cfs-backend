using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Models.Graph;
using CalculateFunding.Services.Calcs.Analysis;
using CalculateFunding.Services.Calcs.Interfaces;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Polly;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Calcs.UnitTests.Analysis
{
    [TestClass]
    public class ReIndexSpecificationCalculationRelationshipsTests
    {
        private Mock<ISpecificationCalculationAnalysis> _analysis;
        private Mock<IReIndexGraphRepository> _relationships;
        private Mock<IJobManagement> _jobManagement;
        private Mock<ICacheProvider> _cacheProvider;

        private ServiceBusReceivedMessage _message;
        
        private ReIndexSpecificationCalculationRelationships _reIndexer;

        [TestInitialize]
        public void SetUp()
        {
            _analysis = new Mock<ISpecificationCalculationAnalysis>();
            _relationships = new Mock<IReIndexGraphRepository>();
            _jobManagement = new Mock<IJobManagement>();
            _cacheProvider = new Mock<ICacheProvider>();


            _message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: new BinaryData(string.Empty),
                messageId: Guid.NewGuid().ToString());

            _reIndexer = new ReIndexSpecificationCalculationRelationships(_analysis.Object,
                _relationships.Object,
                new Mock<ILogger>().Object,
                _jobManagement.Object,
                new ResiliencePolicies { CacheProviderPolicy = Policy.NoOpAsync() },
                _cacheProvider.Object);
        }

        [TestMethod]
        public void GuardsAgainstNoMessageBeingSupplied()
        {
            _message = null;

            Func<Task> invocation = WhenTheReIndexerIsRun;

            invocation
                .Should()
                .Throw<ArgumentNullException>()
                .And
                .ParamName
                .Should()
                .Be("message");
        }
        
        [TestMethod]
        public void GuardsAgainstNoSpecificationIdPropertyInTheSuppliedMessage()
        {
            Func<Task> invocation = WhenTheReIndexerIsRun;

            invocation
                .Should()
                .Throw<ArgumentNullException>()
                .And
                .ParamName
                .Should()
                .Be("specificationId");
        }

        [TestMethod]
        public async Task UpsertsRelationshipsFoundForSpecificationId()
        {
            string specificationId = new RandomString();
            string cacheKey = $"{CacheKeys.CircularDependencies}{specificationId}";
            string jobId = new RandomString();
            SpecificationCalculationRelationships specificationCalculationRelationships = new SpecificationCalculationRelationships();
            SpecificationCalculationRelationships specificationCalculationUnusedRelationships = new SpecificationCalculationRelationships();

            var messageProperties = new Dictionary<string, object>
            {
                ["specification-id"] = specificationId,
                ["jobId"] = jobId
            };

            GivenTheMessageProperties(messageProperties);
            AndTheRelationshipsForSpecificationId(specificationId, specificationCalculationRelationships);
            AndTheUnusedRelationshipsForSpecificationId(specificationCalculationRelationships, specificationCalculationUnusedRelationships);

            await WhenTheReIndexerIsRun();
            
            _cacheProvider.Verify(_ => _.RemoveByPatternAsync(cacheKey),
                Times.Once);

            _relationships.Verify(_ => _.RecreateGraph(specificationCalculationRelationships, specificationCalculationUnusedRelationships),
                Times.Once);
        }

        private void AndTheRelationshipsForSpecificationId(string specificationId,
            SpecificationCalculationRelationships specificationCalculationRelationships)
        {
            _analysis.Setup(_ => _.GetSpecificationCalculationRelationships(specificationId))
                .ReturnsAsync(specificationCalculationRelationships);
        }

        private void AndTheUnusedRelationshipsForSpecificationId(SpecificationCalculationRelationships specificationCalculationRelationships,
            SpecificationCalculationRelationships specificationUnusedCalculationRelationships)
        {
            _relationships.Setup(_ => _.GetUnusedRelationships(specificationCalculationRelationships))
                .ReturnsAsync(specificationUnusedCalculationRelationships);
        }

        private async Task WhenTheReIndexerIsRun()
        {
            await _reIndexer.Run(_message);
        }

        private void GivenTheMessageProperties(IDictionary<string, object> properties)
        {
            IDictionary<string, object> merged = _message?.ApplicationProperties?
                .ToDictionary(kv => kv.Key, kv => kv.Value)
                ?? new Dictionary<string, object>();

            foreach ((string key, object value) in properties)
            {
                merged[key] = value;
            }

            _message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: _message?.Body ?? new BinaryData(string.Empty),
                properties: merged,
                sessionId: Guid.NewGuid().ToString());
        }
    }
}