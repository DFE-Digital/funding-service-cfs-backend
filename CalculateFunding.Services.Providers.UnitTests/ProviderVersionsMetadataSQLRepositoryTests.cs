using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Model = CalculateFunding.Models.Providers;

namespace CalculateFunding.Services.Providers.UnitTests
{
    [TestClass]
    public class ProviderVersionsMetadataSQLRepositoryTests
    {
        private CfsDbContext _dbContext;
        private ProviderVersionsMetadataRepository _repository;
        private Mock<IUnitOfWork> _mockUow;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<CfsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

            _dbContext = new CfsDbContext(options);

            // Real repositories
            var providerVersionRepo = new GenericRepository<ProviderVersionMetadatum>(_dbContext);
            var fundingStreamRepo = new GenericRepository<FundingStream>(_dbContext);
            var currentProviderVersionRepo = new GenericRepository<CurrentProviderVersion>(_dbContext);
            var fundingPeriodRepo = new GenericRepository<FundingPeriod>(_dbContext);
            var fundingPeriodDetailRepo = new GenericRepository<ProviderVersionFundingPeriodDetail>(_dbContext);

            _mockUow = new Mock<IUnitOfWork>();
            _mockUow.Setup(u => u.context).Returns(_dbContext);
            _mockUow.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
            _mockUow.Setup(u => u.GenericRepository<ProviderVersionMetadatum>()).Returns(providerVersionRepo);
            _mockUow.Setup(u => u.GenericRepository<FundingStream>()).Returns(fundingStreamRepo);
            _mockUow.Setup(u => u.GenericRepository<CurrentProviderVersion>()).Returns(currentProviderVersionRepo);
            _mockUow.Setup(u => u.GenericRepository<FundingPeriod>()).Returns(fundingPeriodRepo);
            _mockUow.Setup(u => u.GenericRepository<ProviderVersionFundingPeriodDetail>()).Returns(fundingPeriodDetailRepo);

            _repository = new ProviderVersionsMetadataRepository(_mockUow.Object);
        }

        [TestMethod]
        public async Task CreateProviderVersion_WhenCalled_UpsertsMetadata()
        {
            await _dbContext.FundingStreams.AddAsync(new FundingStream
            {
                FundingStreamId = 1,
                FundingStreamCode = "FS1",
                FundingStreamName = "Funding Stream 1"
            });
            await _dbContext.SaveChangesAsync();
            var providerVersion = new Model.ProviderVersion
            {
                Id = $"providerVersion-prov-1",
                ProviderVersionId = "prov-1",
                VersionType = Model.ProviderVersionType.SystemImported,
                Name = "Sample",
                Description = "desc",
                Version = 1,
                TargetDate = DateTimeOffset.UtcNow,
                Created = DateTimeOffset.UtcNow,
                FundingStream = "FS1",
                ValidationResult = "valid"
            };

            var result = await _repository.CreateProviderVersion(providerVersion);

            result.Should().Be(HttpStatusCode.OK);
        }

        [TestMethod]
        public async Task Exists_WhenMatchingRecordExists_ReturnsTrue()
        {
            var stream = new FundingStream { FundingStreamCode = "FSX", FundingStreamId = 10, FundingStreamName = "Funding Stream X" };
            await _dbContext.FundingStreams.AddAsync(stream);
            await _dbContext.ProviderVersionMetadata.AddAsync(new ProviderVersionMetadatum
            {
                ProviderVersionMetadataId = "ProvVerMetaData-1",
                Description = "desc",
                ProviderVersionId = "prov-1",
                ValidationResult = "valid",
                VersionType = "Authority",
                Name = "MyVersion",
                Version = 1,
                FundingStreamId = 10,
                IsDeleted = false
            });
            await _dbContext.SaveChangesAsync();

            var result = await _repository.Exists("MyVersion", "Authority", 1, "FSX");

            result.Should().BeTrue();
        }

        [TestMethod]
        public async Task GetMasterProviderVersion_WhenFound_ReturnsMaster()
        {
            var stream = new FundingStream { FundingStreamCode = "FS500", FundingStreamId = 500, FundingStreamName = "Funding Stream 500" };
            await _dbContext.FundingStreams.AddAsync(stream);
            await _dbContext.ProviderVersionMetadata.AddAsync(new ProviderVersionMetadatum
            {
                ProviderVersionMetadataId = "master",
                ProviderVersionId = "prov-123",
                VersionType = "SystemImported",
                Name = "Master Ver",
                Description = "desc",
                Version = 1,
                TargetDate = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                FundingStreamId = 500,
                ValidationResult = "valid"
            });
            await _dbContext.SaveChangesAsync();

            var result = await _repository.GetMasterProviderVersion();

            result.Should().NotBeNull();
            result.ProviderVersionId.Should().Be("prov-123");
            result.FundingStream.Should().Be("FS500");
        }

        [TestMethod]
        public async Task IsHealthOk_WhenDatabaseConnects_ReturnsHealthy()
        {
            var result = await _repository.IsHealthOk();

            result.Dependencies.First().HealthOk.Should().BeTrue();
            result.Dependencies.First().Message.Should().Be("SQL DB Connection");
        }

        [TestMethod]
        public async Task GetCurrentProviderVersion_WhenExists_ReturnsVersion()
        {
            var versionId = "Current_FS1";

            var current = new CurrentProviderVersion
            {
                CurrentProviderVersionId = versionId,
                ProviderVersionId = "pv1",
                ProviderSnapshotId = 100,
                IsDeleted = false
            };

            var fundingPeriod = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "2023", FundingPeriodName= "2023" };

            var detail = new ProviderVersionFundingPeriodDetail
            {
                CurrentProviderVersionId = versionId,
                FundingPeriodId = 1,
                ProviderSnapshotId = 100,
                ProviderVersionId = "pv1"
            };

            await _dbContext.CurrentProviderVersions.AddAsync(current);
            await _dbContext.ProviderVersionFundingPeriodDetails.AddAsync(detail);
            await _dbContext.FundingPeriods.AddAsync(fundingPeriod);
            await _dbContext.SaveChangesAsync();

            var result = await _repository.GetCurrentProviderVersion("FS1");

            result.Should().NotBeNull();
            result.ProviderVersionId.Should().Be("pv1");
            result.ProviderSnapshotId.Should().Be(100);
            result.FundingPeriod.First().FundingPeriodName.Should().Be("2023");
        }

        [TestMethod]
        public async Task UpsertCurrentProviderVersion_WhenNewVersion_Inserts()
        {
            var fundingPeriod = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "2023", FundingPeriodName = "2023" };
            await _dbContext.FundingPeriods.AddAsync(fundingPeriod);
            await _dbContext.SaveChangesAsync();

            var version = new Model.CurrentProviderVersion
            {
                Id = "curr-1",
                ProviderSnapshotId = 200,
                ProviderVersionId = "v1",
                FundingPeriod = new List<Model.ProviderSnapShotByFundingPeriod>{
                    new Model.ProviderSnapShotByFundingPeriod
                    {
                        FundingPeriodName = "2023",
                        ProviderSnapshotId = 200,
                        ProviderVersionId = "v1"
                    }
                }
            };

            var result = await _repository.UpsertCurrentProviderVersion(version);

            result.Should().Be(HttpStatusCode.Created);
        }

        [TestMethod]
        public async Task UpsertMaster_ValidRequest_UpsertsCorrectly()
        {
            var stream = new FundingPeriod { FundingPeriodCode = "2023", FundingPeriodId = 1, FundingPeriodName = "2023" };
            await _dbContext.FundingPeriods.AddAsync(stream);
            await _dbContext.SaveChangesAsync();

            var metadata = new Model.MasterProviderVersion
            {
                ProviderVersionId = "master-v",
                VersionType = Model.ProviderVersionType.SystemImported,
                Name = "Master Version",
                Description = "desc",
                Version = 1,
                TargetDate = DateTimeOffset.UtcNow,
                FundingStream = "2023",
                ValidationResult = "pass",
                Created = DateTimeOffset.UtcNow
            };

            var result = await _repository.UpsertMaster(metadata);

            result.Should().Be(HttpStatusCode.OK);
        }

        [TestMethod]
        public async Task GetProviderVersionMetadata_WhenExists_ReturnsMappedObject()
        {
            var stream = new FundingStream { FundingStreamCode = "FS1", FundingStreamId = 1, FundingStreamName = "Funding Stream 1" };
            await _dbContext.FundingStreams.AddAsync(stream);

            var entity = new ProviderVersionMetadatum
            {
                ProviderVersionMetadataId = "providerVersion-pv-1",
                ProviderVersionId = "v1",
                VersionType = "SystemImported",
                Name = "ver1",
                Description = "desc",
                Version = 1,
                TargetDate = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                FundingStreamId = 1,
                ValidationResult = "valid"
            };
            await _dbContext.ProviderVersionMetadata.AddAsync(entity);
            await _dbContext.SaveChangesAsync();

            var result = await _repository.GetProviderVersionMetadata("pv-1");

            result.Should().NotBeNull();
            result.ProviderVersionId.Should().Be("v1");
            result.FundingStream.Should().Be("FS1");
        }

        [TestMethod]
        public async Task GetProviderVersions_WhenExists_ReturnsList()
        {
            var stream = new FundingStream { FundingStreamCode = "FS1", FundingStreamId = 10, FundingStreamName = "Funding Stream 1" };
            await _dbContext.FundingStreams.AddAsync(stream);

            await _dbContext.ProviderVersionMetadata.AddAsync(new ProviderVersionMetadatum
            {
                ProviderVersionMetadataId = "pv-1",
                ProviderVersionId = "v1",
                VersionType = "SystemImported",
                Name = "ver1",
                Description = "desc",
                Version = 1,
                TargetDate = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                FundingStreamId = 10,
                ValidationResult = "valid"
            });
            await _dbContext.SaveChangesAsync();

            var result = await _repository.GetProviderVersions("FS1");

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
        }

        [TestMethod]
        public async Task UpsertProviderVersionByDate_WhenNotExists_Inserts()
        {
            await _dbContext.FundingStreams.AddAsync(new FundingStream
            {
                FundingStreamId = 999,
                FundingStreamCode = "FS1",
                FundingStreamName = "Funding Stream 1"
            });
            await _dbContext.SaveChangesAsync();

            var metadata = new Model.ProviderVersionByDate
            {
                ProviderVersionId = "pv1",
                VersionType = Model.ProviderVersionType.SystemImported,
                Name = "Version",
                Description = "desc",
                Version = 1,
                TargetDate = DateTimeOffset.UtcNow,
                FundingStream = "FS1",
                Created = DateTimeOffset.UtcNow,
                ValidationResult = "valid",
                Year = 2025,
                Month = 7,
                Day = 18
            };

            var result = await _repository.UpsertProviderVersionByDate(metadata);

            result.Should().Be(HttpStatusCode.OK);
        }
    }
}