using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.CalcEngine;
using CalculateFunding.Services.CalcEngine.Caching;
using CalculateFunding.Services.CalcEngine.Interfaces;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Tests.Common.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;
using Moq;

namespace CalculateFunding.Services.Results.UnitTests.Repositories
{
    [TestClass]
    public class ProviderResultsRepositorySqlTests
    {
        private Mock<IUnitOfWork> _uow;
        private CfsDbContext _dbContext;
        private GenericRepository<ProviderResult> _providerResultRepo;
        private GenericRepository<CalcResult> _calcResultRepo;
        private GenericRepository<FundingLineResult> _fundingLineResultRepo;
        private GenericRepository<Provider> _providerRepo;

        private IResultsApiClient _resultsApiClient;
        private IJobManagement _jobManagement;
        private IProviderResultCalculationsHashProvider _calculationsHashProvider;
        private ILogger _logger;

        private ProviderResultsRepository _repository;

        [TestInitialize]
        public void SetUp()
        {
            _uow = new Mock<IUnitOfWork>();

            var options = new DbContextOptionsBuilder<CfsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _dbContext = new CfsDbContext(options);

            _providerResultRepo = new GenericRepository<ProviderResult>(_dbContext);
            _calcResultRepo = new GenericRepository<CalcResult>(_dbContext);
            _fundingLineResultRepo = new GenericRepository<FundingLineResult>(_dbContext);
            _providerRepo = new GenericRepository<Provider>(_dbContext);

            _uow.SetupGet(u => u.context).Returns(_dbContext);
            _uow.Setup(u => u.GenericRepository<ProviderResult>()).Returns(_providerResultRepo);
            _uow.Setup(u => u.GenericRepository<CalcResult>()).Returns(_calcResultRepo);
            _uow.Setup(u => u.GenericRepository<FundingLineResult>()).Returns(_fundingLineResultRepo);
            _uow.Setup(u => u.GenericRepository<Provider>()).Returns(_providerRepo);
            _uow.Setup(u => u.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());

            _resultsApiClient = Substitute.For<IResultsApiClient>();
            _jobManagement = Substitute.For<IJobManagement>();
            _calculationsHashProvider = Substitute.For<IProviderResultCalculationsHashProvider>();
            _calculationsHashProvider.TryUpdateCalculationResultHash(Arg.Any<CalculateFunding.Models.Calcs.ProviderResult>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);
            _logger = Substitute.For<ILogger>();

            _repository = new ProviderResultsRepository(
                _uow.Object,
                _logger,
                _calculationsHashProvider,
                CalcEngineResilienceTestHelper.GenerateTestPolicies(),
                _resultsApiClient,
                _jobManagement);
        }

        [TestMethod]
        public async Task SaveProviderResults_WhenResults_ThenResultsSavedToSqlAndJobsQueued()
        {
            // Arrange
            var spec = new SpecificationSummary { Id = "spec-1", Name = "spec name", FundingPeriod = new Reference { Id = "fp" }, FundingStreams = new[] { new Reference { Id = "fs1" } } };

            var providerResultModel = new CalculateFunding.Models.Calcs.ProviderResult
            {
                Id = Guid.NewGuid().ToString(),
                SpecificationId = spec.Id,
                Provider = new CalculateFunding.Models.ProviderLegacy.ProviderSummary { Id = "prov-1", Name = "prov 1" },
                CalculationResults = new List<CalculationResult>
                {
                    new CalculationResult { Calculation = new Reference { Id = "calc1", Name = "c1" }, CalculationType = CalculateFunding.Models.Calcs.CalculationType.Template, Value = 123.45M }
                },
                FundingLineResults = new List<FundingLineResult>
                {
                    new FundingLineResult { FundingLine = new Reference { Id = "fl1", Name = "fl1" }, FundingLineFundingStreamId = "FS1", Value = 10M }
                }
            };

            // Act
            var result = await _repository.SaveProviderResults(new[] { providerResultModel }, spec, 1, 1, new Reference("u","n"), "corr", "parent");

            // Assert - check DB
            var pr = await _dbContext.ProviderResults.SingleOrDefaultAsync(x => x.ProviderResultId == providerResultModel.Id);
            pr.Should().NotBeNull();

            var provider = await _dbContext.Providers.SingleOrDefaultAsync(x => x.ProviderId == "prov-1");
            provider.Should().NotBeNull();

            var calc = await _dbContext.CalcResults.SingleOrDefaultAsync(x => x.ProviderResultId == providerResultModel.Id && x.CalculationId == "calc1");
            calc.Should().NotBeNull();
            calc.Value.Should().Be("123.45");

            var fr = await _dbContext.FundingLineResults.SingleOrDefaultAsync(x => x.ProviderResultId == providerResultModel.Id && x.FundingLineId == "fl1");
            fr.Should().NotBeNull();
            fr.Value.Should().Be("10");

            // job queued
            await _jobManagement.Received(1).QueueJob(Arg.Any<Common.ApiClient.Jobs.Models.JobCreateModel>());
            await _resultsApiClient.Received(1).QueueMergeSpecificationInformationJob(Arg.Any<Common.ApiClient.Results.Models.MergeSpecificationInformationRequest>());
        }

        [TestMethod]
        public async Task SaveProviderResults_WhenResultsNotChanged_ThenNotSavedOrQueued()
        {
            // Arrange
            _calculationsHashProvider.TryUpdateCalculationResultHash(Arg.Any<CalculateFunding.Models.Calcs.ProviderResult>(), Arg.Any<int>(), Arg.Any<int>()).Returns(false);

            var spec = new SpecificationSummary { Id = "spec-2", Name = "spec name", FundingPeriod = new Reference { Id = "fp" } };

            var providerResultModel = new CalculateFunding.Models.Calcs.ProviderResult
            {
                Id = Guid.NewGuid().ToString(),
                SpecificationId = spec.Id,
                Provider = new CalculateFunding.Models.ProviderLegacy.ProviderSummary { Id = "prov-2", Name = "prov 2" },
                CalculationResults = new List<CalculationResult>
                {
                    new CalculationResult { Calculation = new Reference { Id = "calc2", Name = "c2" }, CalculationType = CalculateFunding.Models.Calcs.CalculationType.Template, Value = 1M }
                }
            };

            // Act
            var result = await _repository.SaveProviderResults(new[] { providerResultModel }, spec, 1, 1, new Reference("u","n"), "corr", "parent");

            // Assert - nothing saved
            (await _dbContext.ProviderResults.ToListAsync()).Should().BeEmpty();
            await _jobManagement.DidNotReceive().QueueJob(Arg.Any<Common.ApiClient.Jobs.Models.JobCreateModel>());
            await _resultsApiClient.DidNotReceive().QueueMergeSpecificationInformationJob(Arg.Any<Common.ApiClient.Results.Models.MergeSpecificationInformationRequest>());
        }
    }
}
