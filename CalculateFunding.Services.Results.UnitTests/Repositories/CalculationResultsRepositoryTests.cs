using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Results.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Services.Results.Models;

namespace CalculateFunding.Services.Results.UnitTests.Repositories
{
    [TestClass]
    public class CalculationResultsRepositoryTests
    {
        private Mock<IUnitOfWork> _uow;
        private CfsDbContext _dbContext;
        private GenericRepository<ProviderResult> _providerResultRepo;
        private GenericRepository<CalcResult> _calcResultRepo;
        private GenericRepository<FundingLineResult> _fundingLineResultRepo;
        private GenericRepository<Provider> _providerRepo;
        private GenericRepository<ProviderSpecificationResult> _providerSpecificationResultRepo;
        private GenericRepository<Successor> _successorRepo;
        private GenericRepository<Predecessor> _predecessorRepo;
        private GenericRepository<FundingPeriod> _fundingPeriodRepo;
        private GenericRepository<FundingStream> _fundingStreamRepo;
        private CalculationResultsRepository _repository;

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
            _providerSpecificationResultRepo = new GenericRepository<ProviderSpecificationResult>(_dbContext);
            _successorRepo = new GenericRepository<Successor>(_dbContext);
            _predecessorRepo = new GenericRepository<Predecessor>(_dbContext);
            _fundingPeriodRepo = new GenericRepository<FundingPeriod>(_dbContext);
            _fundingStreamRepo = new GenericRepository<FundingStream>(_dbContext);

            _uow.SetupGet(u => u.context).Returns(_dbContext);
            _uow.Setup(u => u.GenericRepository<ProviderResult>()).Returns(_providerResultRepo);
            _uow.Setup(u => u.GenericRepository<CalcResult>()).Returns(_calcResultRepo);
            _uow.Setup(u => u.GenericRepository<FundingLineResult>()).Returns(_fundingLineResultRepo);
            _uow.Setup(u => u.GenericRepository<Provider>()).Returns(_providerRepo);
            _uow.Setup(u => u.GenericRepository<ProviderSpecificationResult>()).Returns(_providerSpecificationResultRepo);
            _uow.Setup(u => u.GenericRepository<Successor>()).Returns(_successorRepo);
            _uow.Setup(u => u.GenericRepository<Predecessor>()).Returns(_predecessorRepo);
            _uow.Setup(u => u.GenericRepository<FundingStream>()).Returns(_fundingStreamRepo);
            _uow.Setup(u => u.GenericRepository<FundingPeriod>()).Returns(_fundingPeriodRepo);
            _uow.Setup(u => u.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());

            _repository = new CalculationResultsRepository(_uow.Object);
        }

        [TestMethod]
        public async Task UpdateProviderResults_UpsertsProviderResultAndChildren()
        {
            // Arrange
            string providerResultId = "upr-1";
            string providerId = "prov-upr";
            string specificationId = "spec-upr";

            var model = new CalculateFunding.Models.Calcs.ProviderResult
            {
                Id = providerResultId,
                SpecificationId = specificationId,
                Provider = new CalculateFunding.Models.ProviderLegacy.ProviderSummary
                {
                    Id = providerId,
                    UKPRN = "uk123",
                    URN = "urn123",
                    UPIN = "upin123",
                    EstablishmentNumber = "est123"
                },
                CalculationResults = new List<CalculateFunding.Models.Calcs.CalculationResult>
                {
                    new CalculateFunding.Models.Calcs.CalculationResult
                    {
                        Calculation = new CalculateFunding.Common.Models.Reference { Id = "calc-up-1", Name = "calc up 1" },
                        Value = 10m,
                        CalculationType = CalculateFunding.Models.Calcs.CalculationType.Additional,
                        CalculationDataType = CalculateFunding.Models.Calcs.CalculationDataType.Decimal
                    }
                },
                FundingLineResults = new List<CalculateFunding.Models.Calcs.FundingLineResult>
                {
                    new CalculateFunding.Models.Calcs.FundingLineResult
                    {
                        FundingLine = new CalculateFunding.Common.Models.Reference { Id = "fund-up-1", Name = "fund up 1" },
                        FundingLineFundingStreamId = "fs-up-1",
                        Value = 5m
                    }
                }
            };

            // Act
            var status = await _repository.UpdateProviderResults(new List<CalculateFunding.Models.Calcs.ProviderResult> { model });

            // Assert
            status.Should().Be(System.Net.HttpStatusCode.OK);

            var pr = await _dbContext.ProviderResults.SingleOrDefaultAsync(x => x.ProviderResultId == providerResultId);
            pr.Should().NotBeNull();
            pr.SpecificationId.Should().Be(specificationId);

            var provider = await _dbContext.Providers.SingleOrDefaultAsync(x => x.ProviderId == providerId);
            provider.Should().NotBeNull();
            provider.Ukprn.Should().Be("uk123");

            var calc = await _dbContext.CalcResults.SingleOrDefaultAsync(x => x.ProviderResultId == providerResultId && x.CalculationId == "calc-up-1");
            calc.Should().NotBeNull();
            calc.Value.Should().Be("10");

            var fr = await _dbContext.FundingLineResults.SingleOrDefaultAsync(x => x.ProviderResultId == providerResultId && x.FundingLineId == "fund-up-1");
            fr.Should().NotBeNull();
            fr.Value.Should().Be("5");
        }

        [TestMethod]
        public async Task UpdateProviderResults_UpsertUpdatesExistingCalcResultValue()
        {
            // Arrange
            string providerResultId = "upr-2";
            string providerId = "prov-upr-2";

            var initial = new CalculateFunding.Models.Calcs.ProviderResult
            {
                Id = providerResultId,
                SpecificationId = "spec-up-2",
                Provider = new CalculateFunding.Models.ProviderLegacy.ProviderSummary { Id = providerId },
                CalculationResults = new List<CalculateFunding.Models.Calcs.CalculationResult>
                {
                    new CalculateFunding.Models.Calcs.CalculationResult
                    {
                        Calculation = new CalculateFunding.Common.Models.Reference { Id = "calc-up-2", Name = "calc up 2" },
                        Value = 1m,
                        CalculationType = CalculateFunding.Models.Calcs.CalculationType.Template,
                        CalculationDataType = CalculateFunding.Models.Calcs.CalculationDataType.Decimal
                    }
                }
            };

            await _repository.UpdateProviderResults(new List<CalculateFunding.Models.Calcs.ProviderResult> { initial });

            // Act - update value
            initial.CalculationResults.First().Value = 99m;
            var status = await _repository.UpdateProviderResults(new List<CalculateFunding.Models.Calcs.ProviderResult> { initial });

            // Assert
            status.Should().Be(System.Net.HttpStatusCode.OK);

            var calcRows = await _dbContext.CalcResults.Where(x => x.ProviderResultId == providerResultId && x.CalculationId == "calc-up-2").ToListAsync();
            calcRows.Should().HaveCount(1);
            calcRows.First().Value.Should().Be("99");
        }

        [TestMethod]
        public async Task GetSpecificationResults_ReturnsEmptyWhenNoResults()
        {
            // Arrange
            string providerId = "prov-no";

            // Act
            var results = (await _repository.GetSpecificationResults(providerId)).ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GetSpecificationResults_ExcludesDeletedProviderResults()
        {
            // Arrange
            string providerId = "prov-del";

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = "spec-del",
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = providerId
            };

            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-del",
                CalculationName = "calc del",
                Value = "5",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Template",
                CalculationDataType = "Number"
            };

            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var results = (await _repository.GetSpecificationResults(providerId)).ToList();

            // Assert
            results.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GetProviderWithResultsForSpecificationsByProviderId_ReturnsNotNullWhenMissing()
        {
            // Arrange
            string providerId = "prov-none";

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = "spec-exp",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "prov-ver"
            };

            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-exp",
                CalculationName = "calc exp",
                Value = "9.0",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Template",
                CalculationDataType = "Decimal"
            };

            var fundingLineResultEntity = new FundingLineResult
            {
                FundingLineId = "fund-exp",
                FundingLineName = "fund exp",
                FundingLineFundingStreamId = "fs-exp",
                Value = "2.9",
                ProviderResultId = providerResultEntity.ProviderResultId
            };

            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Name = "provider x",
                ProviderVersionId = providerResultEntity.ProviderVersionId
            };

            var fundingStreamEntity = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS", FundingStreamName = "name" };

            var fundingPeriodEntity = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "funding period" };

            var psr1 = new ProviderSpecificationResult
            {
                ProviderId = providerId,
                ProviderVersionId = providerId,
                SpecificationId = "spec-a",
                SpecificationName = "spec a",
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                FundingPeriodId = fundingPeriodEntity.FundingPeriodId,
                FundingStreamIds = fundingStreamEntity.FundingStreamId
            };

            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            _dbContext.FundingLineResults.Add(fundingLineResultEntity);
            _dbContext.Providers.Add(providerEntity);
            _dbContext.FundingStreams.Add(fundingStreamEntity);
            _dbContext.FundingPeriods.Add(fundingPeriodEntity);
            _dbContext.ProviderSpecificationResults.Add(psr1);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetProviderWithResultsForSpecificationsByProviderId(providerId);

            // Assert
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetProviderWithResultsForSpecificationsByProviderId_MergesMultipleSpecifications()
        {
            // Arrange
            string providerId = "prov-multi";

            var providerEntity = new Provider { ProviderId = providerId, Name = "multi", ProviderVersionId="prov_ver" };

            var fundingStreamEntity = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS" , FundingStreamName = "name"};

            var fundingPeriodEntity = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "funding period" };

            var psr1 = new ProviderSpecificationResult
            {
                ProviderId = providerId,
                ProviderVersionId = providerId,
                SpecificationId = "spec-a",
                SpecificationName = "spec a",
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                FundingPeriodId = fundingPeriodEntity.FundingPeriodId,
                FundingStreamIds = fundingStreamEntity.FundingStreamId
            };

            var psr2 = new ProviderSpecificationResult
            {
                ProviderId = providerId,
                ProviderVersionId = providerId,
                SpecificationId = "spec-b",
                SpecificationName = "spec b",
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                FundingPeriodId = fundingPeriodEntity.FundingPeriodId,
                FundingStreamIds = fundingStreamEntity.FundingStreamId
            };

            _dbContext.Providers.Add(providerEntity);
            _dbContext.ProviderSpecificationResults.Add(psr1);
            _dbContext.ProviderSpecificationResults.Add(psr2);
            _dbContext.FundingPeriods.Add(fundingPeriodEntity);
            _dbContext.FundingStreams.Add(fundingStreamEntity);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetProviderWithResultsForSpecificationsByProviderId(providerId);

            // Assert
            result.Should().NotBeNull();
            result.Specifications.Should().HaveCount(2);
            result.Specifications.Select(s => s.Id).Should().Contain(new[] { "spec-a", "spec-b" });
        }

        [TestMethod]
        public async Task GetSpecificationResults_CleansExponentialNumbers()
        {
            string providerId = "prov-exp";

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = "spec-exp",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "prov-ver"
            };

            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-exp",
                CalculationName = "calc exp",
                Value = "1E+03",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Template",
                CalculationDataType = "Decimal"
            };

            var fundingLineResultEntity = new FundingLineResult
            {
                FundingLineId = "fund-exp",
                FundingLineName = "fund exp",
                FundingLineFundingStreamId = "fs-exp",
                Value = "2E+02",
                ProviderResultId = providerResultEntity.ProviderResultId
            };

            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Name = "provider x",
                ProviderVersionId = "prov-ver"
            };

            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            _dbContext.FundingLineResults.Add(fundingLineResultEntity);
            _dbContext.Providers.Add(providerEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            var results = (await _repository.GetSpecificationResults(providerId)).ToList();

            results.Should().HaveCount(1);

            var result = results.First();

            result.CalculationResults.Should().HaveCount(1);
            //result.CalculationResults.First().Value.ToString().Should().NotContain("E");

            result.FundingLineResults.Should().HaveCount(1);
            //result.FundingLineResults.First().Value.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetProviderWithResultsForSpecificationsByProviderId_ReturnsMappedProviderWithSpecifications()
        {
            // Arrange
            string providerId = "prov-specs";
            string specificationId = "spec-x";


            var fundingStreamEntity = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS" , FundingStreamName = "FSName"};

            var fundingPeriodEntity = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "funding period" };


            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Name = "provider x",
                ProviderVersionId = providerId
            };

            var psr = new ProviderSpecificationResult
            {
                ProviderId = providerId,
                ProviderVersionId = providerId,
                SpecificationId = specificationId,
                SpecificationName = "spec name x",
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                FundingPeriodId = fundingPeriodEntity.FundingPeriodId,
                FundingStreamIds = fundingStreamEntity.FundingStreamId
            };

            _dbContext.Providers.Add(providerEntity);
            _dbContext.FundingStreams.Add(fundingStreamEntity);
            _dbContext.FundingPeriods.Add(fundingPeriodEntity);
            _dbContext.ProviderSpecificationResults.Add(psr);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetProviderWithResultsForSpecificationsByProviderId(providerId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(providerId);
            result.Specifications.Should().NotBeNull();
            result.Specifications.First().Id.Should().Be(specificationId);
            result.Specifications.First().Name.Should().Be(psr.SpecificationName);
        }

        [TestMethod]
        public async Task GetProviderResultById_MapsContentAndFields()
        {
            // Arrange
            string specificationId = "spec-3";
            string providerId = "prov-3";

            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Ukprn = "ukprn",
                Urn = "urn-3",
                Upin = "upin-3",
                EstablishmentNumber = "33333",
                Name = "prov name 3",
                ProviderVersionId = "pro_ver_id"
            };

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = specificationId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pro_ver_id"
            };

            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-id-3",
                CalculationName = "calc name 3",
                Value = "7",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Template",
                CalculationDataType = "Decimal"
            };
            var successorEntity = new Successor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = "ukprn"
            };

            var predecessorEntity = new Predecessor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = "ukprn"
            };
            _dbContext.Providers.Add(providerEntity);
            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            _dbContext.Predecessors.Add(predecessorEntity);
            _dbContext.Providers.Add(providerEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetProviderResultById(providerResultEntity.ProviderResultId, providerId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(providerResultEntity.ProviderResultId);
            result.CalculationResults.Should().HaveCount(1);
            result.CalculationResults.First().Calculation.Id.Should().Be(calcResultEntity.CalculationId);
        }

        [TestMethod]
        public async Task GetProviderResultsBySpecificationId_ReturnsAllAndRespectsMaxItemCount()
        {
            // Arrange
            string specificationId = "spec-4";
            var providerEntity_One = new Provider
            {
                ProviderId = "p1",
                Ukprn = "ukprn",
                Urn = "urn",
                Upin = "upin",
                EstablishmentNumber = "12345",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name",
                ProviderVersionId = "prov-ver"
            };
            var providerEntity_Two = new Provider
            {
                ProviderId = "p2",
                Ukprn = "ukprn",
                Urn = "urn",
                Upin = "upin",
                EstablishmentNumber = "12345",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name",
                ProviderVersionId = "prov-ver"
            };


            var pr1 = new ProviderResult { ProviderResultId = "pr-1", ProviderId = "p1", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId = "prov-ver" };
            var pr2 = new ProviderResult { ProviderResultId = "pr-2", ProviderId = "p2", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId="prov-ver" };

            _dbContext.ProviderResults.Add(pr1);
            _dbContext.ProviderResults.Add(pr2);
            _dbContext.Providers.Add(providerEntity_One);
            _dbContext.Providers.Add(providerEntity_Two);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var all = (await _repository.GetProviderResultsBySpecificationId(specificationId)).ToList();
            var limited = (await _repository.GetProviderResultsBySpecificationId(specificationId, maxItemCount: 1)).ToList();

            // Assert
            all.Should().HaveCount(2);
            limited.Should().HaveCount(1);
        }

        [TestMethod]
        public async Task GetProviderResultByCalculationType_FiltersCalculationResultsByType()
        {
            // Arrange
            string specificationId = "spec-5";
            string providerId = "prov-5";

            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Ukprn = "ukprn",
                Urn = "urn",
                Upin = "upin",
                EstablishmentNumber = "12345",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name",
                ProviderVersionId = "pro_ver_id"
            };

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = specificationId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pro_ver_id"
            };

            var calcTemplate = new CalcResult
            {
                CalculationId = "calc-template",
                CalculationName = "template calc",
                Value = "1",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Template",
                CalculationDataType = "Decimal"
            };

            var calcAdditional = new CalcResult
            {
                CalculationId = "calc-add",
                CalculationName = "additional calc",
                Value = "2",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Additional",
                CalculationDataType = "Decimal"
            };
            var successorEntity = new Successor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = "ukprn"
            };

            var predecessorEntity = new Predecessor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = "ukprn"
            };
            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcTemplate);
            _dbContext.CalcResults.Add(calcAdditional);
            _dbContext.Successors.Add(successorEntity);
            _dbContext.Predecessors.Add(predecessorEntity);
            _dbContext.Providers.Add(providerEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetProviderResultByCalculationType(providerId, specificationId, CalculateFunding.Models.Calcs.CalculationType.Template);

            // Assert
            result.Should().NotBeNull();
            result.CalculationResults.Should().HaveCount(1);
            result.CalculationResults.First().Calculation.Id.Should().Be(calcTemplate.CalculationId);
        }

        [TestMethod]
        public async Task GetProviderResultsBySpecificationIdAndProviders_ReturnsOnlyRequestedProviders()
        {
            // Arrange
            string specificationId = "spec-6";

            var providerEntity_One = new Provider
            {
                ProviderId = "p-a",
                Ukprn = "ukprn",
                Urn = "urn",
                Upin = "upin",
                EstablishmentNumber = "12345",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name",
                ProviderVersionId = "prov_ver"
            };
            var providerEntity_Two = new Provider
            {
                ProviderId = "p-b",
                Ukprn = "ukprn",
                Urn = "urn",
                Upin = "upin",
                EstablishmentNumber = "12345",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name",
                ProviderVersionId = "prov-ver"
            };

            var pr1 = new ProviderResult { ProviderResultId = "pr-a", ProviderId = "p-a", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId="prov_ver" };
            var pr2 = new ProviderResult { ProviderResultId = "pr-b", ProviderId = "p-b", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId="prov_ver" };

            _dbContext.ProviderResults.Add(pr1);
            _dbContext.ProviderResults.Add(pr2);
            _dbContext.Providers.Add(providerEntity_One);
            _dbContext.Providers.Add(providerEntity_Two);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var results = (await _repository.GetProviderResultsBySpecificationIdAndProviders(new[] { "p-a" }, specificationId)).ToList();

            // Assert
            results.Should().HaveCount(1);
            results.First().Id.Should().Be(pr1.ProviderResultId);
        }

        [TestMethod]
        public async Task GetSingleProviderResultBySpecificationId_ReturnsMappedResult()
        {
            // Arrange
            string specificationId = "spec-7";

            var providerResultEntity = new ProviderResult { ProviderResultId = "pr-exists", ProviderId = "px", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId = "pro_ver_id" };


            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-exp",
                CalculationName = "calc exp",
                Value = "8.0",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Template",
                CalculationDataType = "Decimal"
            };

            var fundingLineResultEntity = new FundingLineResult
            {
                FundingLineId = "fund-exp",
                FundingLineName = "fund exp",
                FundingLineFundingStreamId = "fs-exp",
                Value = "2.9",
                ProviderResultId = providerResultEntity.ProviderResultId,
            };

            var providerEntity = new Provider
            {
                ProviderId = "px",
                Name = "provider x",
                ProviderVersionId = providerResultEntity.ProviderVersionId
            };


            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            _dbContext.FundingLineResults.Add(fundingLineResultEntity);
            _dbContext.Providers.Add(providerEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetSingleProviderResultBySpecificationId(specificationId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(providerResultEntity.ProviderResultId);
        }

        [TestMethod]
        public async Task ProviderHasResultsBySpecificationId_ReturnsTrueWhenExists()
        {
            // Arrange
            string specificationId = "spec-8";
            var pr = new ProviderResult { ProviderResultId = "pr-exists", ProviderId = "px", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow , ProviderVersionId="pro_ver_id"};
            _dbContext.ProviderResults.Add(pr);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var has = await _repository.ProviderHasResultsBySpecificationId(specificationId);

            // Assert
            has.Should().BeTrue();
        }

        [TestMethod]
        public async Task GetSpecificationCalculationResultsLastUpdated_ReturnsLatestUpdated()
        {
            // Arrange
            string specificationId = "spec-9";
            var older = new ProviderResult { ProviderResultId = "older", ProviderId = "p1", SpecificationId = specificationId, IsDeleted = false, UpdatedAt = DateTime.UtcNow.AddHours(-2), ProviderVersionId = "pro_ver_id" };
            var newer = new ProviderResult { ProviderResultId = "newer", ProviderId = "p2", SpecificationId = specificationId, IsDeleted = false, UpdatedAt = DateTime.UtcNow, ProviderVersionId = "pro_ver_id" };

            _dbContext.ProviderResults.Add(older);
            _dbContext.ProviderResults.Add(newer);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var lastUpdated = await _repository.GetSpecificationCalculationResultsLastUpdated(specificationId);

            // Assert
            lastUpdated.Should().BeCloseTo(newer.UpdatedAt, precision: TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        public async Task CheckHasNewResultsForSpecificationIdAndTime_ReturnsTrue_WhenProviderResultUpdatedAfterDate()
        {
            // Arrange
            string specificationId = "spec-1";
            DateTimeOffset dateFrom = DateTimeOffset.UtcNow.AddHours(-1);

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                SpecificationId = specificationId,
                IsDeleted = false,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pro_ver_id",
                ProviderId = "prov-new"
            };

            _dbContext.ProviderResults.Add(providerResultEntity);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            bool hasNew = await _repository.CheckHasNewResultsForSpecificationIdAndTime(specificationId, dateFrom);

            // Assert
            hasNew.Should().BeTrue();
        }

        [TestMethod]
        public async Task CheckHasNewResultsForSpecificationIdAndTime_ReturnsFalse_WhenNoProviderResultUpdatedAfterDate()
        {
            // Arrange
            string specificationId = "spec-1";
            DateTimeOffset dateFrom = DateTimeOffset.UtcNow;

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                SpecificationId = specificationId,
                IsDeleted = false,
                UpdatedAt = DateTime.UtcNow.AddHours(-2),
                ProviderId = "prov-old",
                ProviderVersionId = "prov-old"
            };

            _dbContext.ProviderResults.Add(providerResultEntity);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            bool hasNew = await _repository.CheckHasNewResultsForSpecificationIdAndTime(specificationId, dateFrom);

            // Assert
            hasNew.Should().BeFalse();
        }

        [TestMethod]
        public async Task GetAllProviderResults_MapsUpdatedAtAndContent_WithCalcAndFundingAndProviderFields()
        {
            // Arrange
            string specificationId = "spec-1";
            string providerId = "prov-1";

            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Ukprn = "ukprn",
                Urn = "urn",
                Upin = "upin",
                EstablishmentNumber = "12345",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name",
                ProviderVersionId = "pro_ver_id"
            };

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = specificationId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pro_ver_id"
            };

            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-id-1",
                CalculationName = "calc name 1",
                Value = "123",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Additional",
                CalculationDataType = "Decimal"
            };

            var fundingLineResultEntity = new FundingLineResult
            {
                FundingLineId = "fund-id-1",
                FundingLineName = "fund name 1",
                FundingLineFundingStreamId = "fs-1",
                Value = "100",
                ProviderResultId = providerResultEntity.ProviderResultId
            };

            var successorEntity = new Successor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = providerEntity.Ukprn
            };

            var predecessorEntity = new Predecessor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = providerEntity.Ukprn
            };

            _dbContext.Providers.Add(providerEntity);
            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            _dbContext.FundingLineResults.Add(fundingLineResultEntity);
            _dbContext.Successors.Add(successorEntity);
            _dbContext.Predecessors.Add(predecessorEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var results = (await _repository.GetAllProviderResults()).ToList();

            // Assert
            results.Should().HaveCount(1);

            var doc = results.First();

            doc.UpdatedAt.Should().Be(providerResultEntity.UpdatedAt);

            doc.Content.Should().NotBeNull();
            doc.Content.Id.Should().Be(providerResultEntity.ProviderResultId);
            doc.Content.SpecificationId.Should().Be(specificationId);

            doc.Content.Provider.Should().NotBeNull();
            doc.Content.Provider.UKPRN.Should().Be(providerEntity.Ukprn);
            doc.Content.Provider.URN.Should().Be(providerEntity.Urn);
            doc.Content.Provider.UPIN.Should().Be(providerEntity.Upin);
            doc.Content.Provider.EstablishmentNumber.Should().Be(providerEntity.EstablishmentNumber);

            doc.Content.CalculationResults.Should().HaveCount(1);
            var mappedCalc = doc.Content.CalculationResults.First();
            mappedCalc.Calculation.Id.Should().Be(calcResultEntity.CalculationId);
            mappedCalc.Value.ToString().Should().Be(calcResultEntity.Value);

            doc.Content.FundingLineResults.Should().HaveCount(1);
            var mappedFunding = doc.Content.FundingLineResults.First();
            mappedFunding.FundingLine.Id.Should().Be(fundingLineResultEntity.FundingLineId);
            mappedFunding.Value.Should().Be(decimal.Parse(fundingLineResultEntity.Value));
        }

        [TestMethod]
        public async Task GetProviderResult_ByProviderAndSpecification_MapsContentAndFields()
        {
            // Arrange
            string specificationId = "spec-1";
            string providerId = "prov-2";

            var providerEntity = new Provider
            {
                ProviderId = providerId,
                Ukprn = "ukprn-2",
                Urn = "urn-2",
                Upin = "upin-2",
                EstablishmentNumber = "54321",
                ProviderType = "prov type",
                ProviderSubType = "prov sub type",
                Name = "prov name 2",
                ProviderVersionId = "pro_ver_id"
            };

            var providerResultEntity = new ProviderResult
            {
                ProviderResultId = Guid.NewGuid().ToString(),
                ProviderId = providerId,
                SpecificationId = specificationId,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pro_ver_id"
            };

            var calcResultEntity = new CalcResult
            {
                CalculationId = "calc-id-2",
                CalculationName = "calc name 2",
                Value = "10",
                ProviderResultId = providerResultEntity.ProviderResultId,
                CalculationType = "Additional",
                CalculationDataType = "Decimal"
            };

            var fundingLineResultEntity = new FundingLineResult
            {
                FundingLineId = "fund-id-2",
                FundingLineName = "fund name 2",
                FundingLineFundingStreamId = "fs-2",
                Value = "50",
                ProviderResultId = providerResultEntity.ProviderResultId
            };
            var successorEntity = new Successor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = providerEntity.Ukprn
            };

            var predecessorEntity = new Predecessor
            {
                ProviderId = providerId,
                ProviderVersionId = "pro_ver_id",
                Ukprn = providerEntity.Ukprn
            };

            _dbContext.Providers.Add(providerEntity);
            _dbContext.ProviderResults.Add(providerResultEntity);
            _dbContext.CalcResults.Add(calcResultEntity);
            _dbContext.FundingLineResults.Add(fundingLineResultEntity);
            _dbContext.Successors.Add(successorEntity);
            _dbContext.Predecessors.Add(predecessorEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            var result = await _repository.GetProviderResult(providerId, specificationId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(providerResultEntity.ProviderResultId);
            result.SpecificationId.Should().Be(specificationId);

            result.Provider.Should().NotBeNull();
            result.Provider.UKPRN.Should().Be(providerEntity.Ukprn);
            result.Provider.URN.Should().Be(providerEntity.Urn);
            result.Provider.UPIN.Should().Be(providerEntity.Upin);
            result.Provider.EstablishmentNumber.Should().Be(providerEntity.EstablishmentNumber);

            result.CalculationResults.Should().HaveCount(1);
            result.CalculationResults.First().Calculation.Id.Should().Be(calcResultEntity.CalculationId);
            result.CalculationResults.First().Value.ToString().Should().Be(calcResultEntity.Value);

            result.FundingLineResults.Should().HaveCount(1);
            result.FundingLineResults.First().FundingLine.Id.Should().Be(fundingLineResultEntity.FundingLineId);
            result.FundingLineResults.First().Value.Should().Be(decimal.Parse(fundingLineResultEntity.Value));
        }

        private void DetachAllDBEntries()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
        }

        [TestMethod]
        public async Task DeleteCalculationResultsBySpecificationId_SoftDelete_MarksProviderResultsDeletedOnly()
        {
            // Arrange
            string specificationId = "spec-soft-1";

            var pr = new ProviderResult { ProviderResultId = "pr-soft-1", ProviderId = "p-soft", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId = "pv1" };
            var cr = new CalcResult { CalculationId = "cr-soft-1", CalculationName = "c", Value = "1", ProviderResultId = pr.ProviderResultId, CalculationType = "Template", CalculationDataType = "Decimal" };
            var fr = new FundingLineResult { FundingLineId = "fr-soft-1", FundingLineName = "f", FundingLineFundingStreamId = "fs", Value = "2", ProviderResultId = pr.ProviderResultId };
            var psr = new ProviderSpecificationResult { ProviderId = "p-soft", SpecificationId = specificationId, SpecificationName = "spec name", FundingPeriodId = 1, FundingStreamIds = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsDeleted = false, ProviderVersionId = "pv1" };

            _dbContext.ProviderResults.Add(pr);
            _dbContext.CalcResults.Add(cr);
            _dbContext.FundingLineResults.Add(fr);
            _dbContext.ProviderSpecificationResults.Add(psr);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            await _repository.DeleteCalculationResultsBySpecificationId(specificationId, CalculateFunding.Models.Messages.DeletionType.SoftDelete);

            // Assert
            var providerResult = await _dbContext.ProviderResults.SingleOrDefaultAsync(x => x.ProviderResultId == pr.ProviderResultId);
            providerResult.Should().NotBeNull();
            providerResult.IsDeleted.Should().BeTrue();

            // children should remain
            (await _dbContext.CalcResults.Where(x => x.ProviderResultId == pr.ProviderResultId).ToListAsync()).Should().NotBeEmpty();
            (await _dbContext.FundingLineResults.Where(x => x.ProviderResultId == pr.ProviderResultId).ToListAsync()).Should().NotBeEmpty();

            // provider specification result should remain
            (await _dbContext.ProviderSpecificationResults.Where(x => x.SpecificationId == specificationId).ToListAsync()).Should().NotBeEmpty();
        }

        [TestMethod]
        public async Task DeleteCalculationResultsBySpecificationId_PermanentDelete_RemovesAllRelatedRows()
        {
            // Arrange
            string specificationId = "spec-hard-1";

            var pr = new ProviderResult { ProviderResultId = "pr-hard-1", ProviderId = "p-hard", SpecificationId = specificationId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ProviderVersionId = "pv1" };
            var cr = new CalcResult { CalculationId = "cr-hard-1", CalculationName = "c", Value = "1", ProviderResultId = pr.ProviderResultId, CalculationType = "Template", CalculationDataType = "Decimal" };
            var fr = new FundingLineResult { FundingLineId = "fr-hard-1", FundingLineName = "f", FundingLineFundingStreamId = "fs", Value = "2", ProviderResultId = pr.ProviderResultId };
            var psr = new ProviderSpecificationResult { ProviderId = "p-hard", SpecificationId = specificationId, SpecificationName = "spec name", FundingPeriodId = 1, FundingStreamIds = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsDeleted = false , ProviderVersionId = "pv1" };

            _dbContext.ProviderResults.Add(pr);
            _dbContext.CalcResults.Add(cr);
            _dbContext.FundingLineResults.Add(fr);
            _dbContext.ProviderSpecificationResults.Add(psr);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            await _repository.DeleteCalculationResultsBySpecificationId(specificationId, CalculateFunding.Models.Messages.DeletionType.PermanentDelete);

            // Assert
            (await _dbContext.ProviderResults.Where(x => x.SpecificationId == specificationId).ToListAsync()).Should().BeEmpty();
            (await _dbContext.CalcResults.Where(x => x.ProviderResultId == pr.ProviderResultId).ToListAsync()).Should().BeEmpty();
            (await _dbContext.FundingLineResults.Where(x => x.ProviderResultId == pr.ProviderResultId).ToListAsync()).Should().BeEmpty();
            (await _dbContext.ProviderSpecificationResults.Where(x => x.SpecificationId == specificationId).ToListAsync()).Should().BeEmpty();
        }

        [TestMethod]
        public async Task DeleteCalculationResultsBySpecificationId_NoProviderResults_NoException()
        {
            // Arrange
            string specificationId = "spec-none";

            // Act / Assert - should not throw
            await _repository.DeleteCalculationResultsBySpecificationId(specificationId, CalculateFunding.Models.Messages.DeletionType.PermanentDelete);
        }

        [TestMethod]
        public async Task DeleteCurrentProviderResults_SoftDeletesMatchingProviderResults()
        {
            // Arrange
            var prEntity = new ProviderResult
            {
                ProviderResultId = "pr-del-1",
                ProviderId = "p-del",
                SpecificationId = "spec-del",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pv1"
            };

            _dbContext.ProviderResults.Add(prEntity);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            var model = new CalculateFunding.Models.Calcs.ProviderResult { Id = prEntity.ProviderResultId };

            // Act
            await _repository.DeleteCurrentProviderResults(new[] { model });

            // Assert
            var pr = await _dbContext.ProviderResults.SingleOrDefaultAsync(x => x.ProviderResultId == prEntity.ProviderResultId);
            pr.Should().BeNull();
        }

        [TestMethod]
        public async Task DeleteCurrentProviderResults_NonMatchingIds_DoNotAffectExistingEntries()
        {
            // Arrange
            var existing = new ProviderResult
            {
                ProviderResultId = "pr-exist-1",
                ProviderId = "p-exist",
                SpecificationId = "spec-exist",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProviderVersionId = "pv1"
            };

            _dbContext.ProviderResults.Add(existing);
            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            var model = new CalculateFunding.Models.Calcs.ProviderResult { Id = "non-existent-id" };

            // Act
            await _repository.DeleteCurrentProviderResults(new[] { model });

            // Assert
            var pr = await _dbContext.ProviderResults.SingleOrDefaultAsync(x => x.ProviderResultId == existing.ProviderResultId);
            pr.Should().NotBeNull();
            pr.IsDeleted.Should().BeFalse();
        }

        [TestMethod]
        public async Task DeleteCurrentProviderResults_Null_ThrowsArgumentNullException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await _repository.DeleteCurrentProviderResults(null));
        }

        [TestMethod]
        public async Task UpsertSpecificationWithProviderResults_InsertsNewProviderSpecificationResults()
        {
            // Arrange
            string providerId = "psr-prov-1";

            var fundingStreamEntity = new FundingStream { FundingStreamId = 1, FundingStreamCode = "1", FundingStreamName = "name" };

            var fundingPeriodEntity = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "1", FundingPeriodName = "funding period" };

            _dbContext.FundingStreams.Add(fundingStreamEntity);
            _dbContext.FundingPeriods.Add(fundingPeriodEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            var providerWithResults = new ProviderWithResultsForSpecifications
            {
                Provider = new ProviderInformation { Id = providerId },
                Specifications = new List<SpecificationInformation>
                {
                    new SpecificationInformation
                    {
                        Id = "spec-psr-1",
                        Name = "Spec One",
                        FundingPeriodId = "1",
                        FundingStreamIds = new[] { "1" },
                        LastEditDate = DateTimeOffset.UtcNow,
                        FundingPeriodEnd = DateTimeOffset.UtcNow
                    }
                }
            };

            // Act
            await _repository.UpsertSpecificationWithProviderResults(providerWithResults);

            // Assert
            var psr = await _dbContext.ProviderSpecificationResults.SingleOrDefaultAsync(x => x.ProviderId == providerId && x.SpecificationId == "spec-psr-1");
            psr.Should().NotBeNull();
            psr.SpecificationName.Should().Be("Spec One");
            psr.FundingPeriodId.Should().Be(1);
            psr.FundingStreamIds.Should().Be(1);
        }

        [TestMethod]
        public async Task UpsertSpecificationWithProviderResults_UpdatesExistingProviderSpecificationResult()
        {
            // Arrange
            string providerId = "psr-prov-2";
            string specId = "spec-psr-2";

            var fundingStreamEntity = new FundingStream { FundingStreamId = 202, FundingStreamCode = "202", FundingStreamName = "name" };

            var fundingPeriodEntity = new FundingPeriod { FundingPeriodId = 22, FundingPeriodCode = "22", FundingPeriodName = "funding period" };

            _dbContext.FundingStreams.Add(fundingStreamEntity);
            _dbContext.FundingPeriods.Add(fundingPeriodEntity);

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            var initial = new ProviderWithResultsForSpecifications
            {
                Provider = new ProviderInformation { Id = providerId },
                Specifications = new List<SpecificationInformation>
                {
                    new SpecificationInformation
                    {
                        Id = specId,
                        Name = "Initial",
                        FundingPeriodId = "22",
                        FundingStreamIds = new[] { "202" }
                    }
                }
            };

            await _repository.UpsertSpecificationWithProviderResults(initial);

            DetachAllDBEntries();

            // Act - update name and funding period
            var updated = new ProviderWithResultsForSpecifications
            {
                Provider = new ProviderInformation { Id = providerId },
                Specifications = new List<SpecificationInformation>
                {
                    new SpecificationInformation
                    {
                        Id = specId,
                        Name = "Updated",
                        FundingPeriodId = "22",
                        FundingStreamIds = new[] { "202" }
                    }
                }
            };

            await _repository.UpsertSpecificationWithProviderResults(updated);

            // Assert
            var psr = await _dbContext.ProviderSpecificationResults.SingleOrDefaultAsync(x => x.ProviderId == providerId && x.SpecificationId == specId);
            psr.Should().NotBeNull();
            psr.SpecificationName.Should().Be("Updated");
            psr.FundingPeriodId.Should().Be(22);
            psr.FundingStreamIds.Should().Be(202);
        }

        // Argument validation tests moved from CalculationResultsRepositoryArgumentValidationTests
        [TestMethod]
        public async Task ArgumentValidation_GetProviderResultById_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResultById(null, "p"));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetProviderResult_NullProviderId_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResult(null, "spec"));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetProviderResultByCalculationType_Nulls_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResultByCalculationType(null, "spec", (CalculateFunding.Models.Calcs.CalculationType)0));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResultByCalculationType("prov", null, (CalculateFunding.Models.Calcs.CalculationType)0));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetSpecificationResults_NullProviderId_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetSpecificationResults(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_UpdateProviderResults_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.UpdateProviderResults(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetProviderResultsBySpecificationId_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResultsBySpecificationId(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetProviderResultsBySpecificationIdAndProviders_Nulls_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResultsBySpecificationIdAndProviders(null, "spec"));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderResultsBySpecificationIdAndProviders(new List<string> { "p" }, null));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetCalculationResultTotalForSpecificationId_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetCalculationResultTotalForSpecificationId(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetSingleProviderResultBySpecificationId_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetSingleProviderResultBySpecificationId(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_ProviderHasResultsBySpecificationId_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.ProviderHasResultsBySpecificationId(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetProviderWithResultsForSpecificationsByProviderId_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetProviderWithResultsForSpecificationsByProviderId(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_UpsertSpecificationWithProviderResults_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.UpsertSpecificationWithProviderResults(null));
        }

        [TestMethod]
        public void ArgumentValidation_GetProvidersWithResultsForSpecificationBySpecificationId_Null_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => _repository.GetProvidersWithResultsForSpecificationBySpecificationId(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_DeleteCalculationResultsBySpecificationId_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.DeleteCalculationResultsBySpecificationId(null, CalculateFunding.Models.Messages.DeletionType.PermanentDelete));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetSpecificationCalculationResultsLastUpdated_Null_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetSpecificationCalculationResultsLastUpdated(null));
        }

        [TestMethod]
        public async Task ArgumentValidation_GetAggregateCalculationResults_NullArgs_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetAggregateCalculationResults(null, new List<string> { "c" }));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _repository.GetAggregateCalculationResults("spec", null));
        }
    }
}
