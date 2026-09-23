using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Models.Messages;
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
using EfcoreProviderSourceDataset = CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderSourceDataset;
using EfcoreProviderSourceDatasetVersion = CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderSourceDatasetVersion;


namespace CalculateFunding.Services.Results.UnitTests.Repositories
{
    [TestClass]
    public class ProviderSourceDatasetRepositoryTest
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected ProviderSourceDatasetRepository _repository;
        protected CfsDbContext _dbContext;

        public ProviderSourceDatasetRepositoryTest()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _repository = new ProviderSourceDatasetRepository(_uowMock.Object);
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        }

        #region GetProviderSourceDatasets
               
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetProviderSourceDatasets_FailedScenario_InvalidData_NullProviderIdThrowsArgumentNullException()
        {
            await _repository.GetProviderSourceDatasets(null, "specId");
        }

        [TestMethod]
        public async Task GetProviderSourceDatasets_EdgeCase_NoMatchingScopedDatasets_ReturnsEmptyList()
        {
            SetupTableProviderSourceDataset();

            var result = await _repository.GetProviderSourceDatasets("providerId", "specId");

            result.Should().BeNullOrEmpty();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetProviderSourceDatasets_ExceptionScenario_ThrowsExceptionOnError()
        {
            await _repository.GetProviderSourceDatasets(null , null);
        }

        #endregion

        #region GetAllScopedProviderIdsForSpecificationId

        [TestMethod]
        public async Task GetAllScopedProviderIdsForSpecificationId_ValidScenario_ReturnsProviderIds()
        {
            SetupProviderSourceDataset();
            
            var result = await _repository.GetAllScopedProviderIdsForSpecificationId("spec-002");
            
            result.Should().NotBeEmpty();
            result.Should().HaveCount(1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetAllScopedProviderIdsForSpecificationId_FailedScenario_InvalidData_NullSpecificationIdThrowsArgumentNullException()
        {
            await _repository.GetAllScopedProviderIdsForSpecificationId(null);
        }

        [TestMethod]
        public async Task GetAllScopedProviderIdsForSpecificationId_EdgeCase_NoMatchingScopedDatasets_ReturnsEmptyList()
        {
            SetupTableProviderSourceDataset();
            
            var result = await _repository.GetAllScopedProviderIdsForSpecificationId("spec-002");

            result.Should().BeNullOrEmpty();
        }
        #endregion

        #region DeleteProviderSourceDataset

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DeleteProviderSourceDataset_FailedScenario_InvalidData_NullProviderDatasetIdThrowsArgumentNullException()
        {
            await _repository.DeleteProviderSourceDataset(null, DeletionType.SoftDelete);
        }
        
        [TestMethod]
        public async Task DeleteProviderSourceDataset_ValidScenario_SoftDeleteUpdatesFlag()
        {
            SetupProviderSourceDataset();

            await _repository.DeleteProviderSourceDataset("ver-001", DeletionType.SoftDelete);
                        
            var updatedRecord = await _dbContext.ProviderSourceDatasets
                .FirstOrDefaultAsync(x => x.ProviderSourceDatasetId == "ver-001");

            updatedRecord.Should().NotBeNull();
            updatedRecord.IsDeleted.Should().BeTrue();
        }

        [TestMethod]
        public async Task DeleteProviderSourceDataset_ValidScenario_PermanentDeleteRemovesRecords()
        {
            SetupProviderSourceDataset();

            await _repository.DeleteProviderSourceDataset("ver-001", DeletionType.PermanentDelete);

            var deletedRecord = await _dbContext.ProviderSourceDatasets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProviderSourceDatasetId == "ver-001");

            deletedRecord.Should().BeNull();
        }
               

        [TestMethod]
        public async Task DeleteProviderSourceDataset_EdgeCase_NoRecordsFound_DoesNotCommit()
        {
            SetupTableProviderSourceDataset();

            await _repository.DeleteProviderSourceDataset("invalidId", DeletionType.SoftDelete);

            _uowMock.Verify(x => x.CommitAsync(), Times.Never);

            var result = await _dbContext.ProviderSourceDatasets.CountAsync();

            result.Should().Be(0);
        }
        
        [TestMethod]
        public async Task DeleteProviderSourceDataset_ExceptionScenario_CommitFails_ThrowsException()
        {
            SetupProviderSourceDataset();

            _uowMock.Setup(x => x.CommitAsync()).ThrowsAsync(new InvalidOperationException("Database connection timed out."));

            var result = async () => await _repository.DeleteProviderSourceDataset("ver-001", DeletionType.PermanentDelete);

            await result.Should().ThrowAsync<InvalidOperationException>().WithMessage("Database connection timed out.");
        }

        #endregion

        #region DeleteProviderSourceDatasetVersion

        [TestMethod]
        public async Task DeleteProviderSourceDatasetVersion_ValidScenario_SoftDeleteUpdatesFlag()
        {
            SetupProviderSourceDatasetVersion();

            await _repository.DeleteProviderSourceDatasetVersion("ver-001", DeletionType.SoftDelete);

            var updatedRecord = await _dbContext.ProviderSourceDatasetVersions
                .FirstOrDefaultAsync(x => x.ProviderSourceDatasetVersionId == "ver-001");

            updatedRecord.Should().NotBeNull();
            updatedRecord.IsDeleted.Should().BeTrue();
        }

        [TestMethod]
        public async Task DeleteProviderSourceDatasetVersion_ValidScenario_PermanentDeleteUpdatesFlag()
        {
            SetupProviderSourceDatasetVersion();

            await _repository.DeleteProviderSourceDatasetVersion("ver-001", DeletionType.PermanentDelete);

            var deletedRecord = await _dbContext.ProviderSourceDatasetVersions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProviderSourceDatasetVersionId == "ver-001");

            deletedRecord.Should().BeNull();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DeleteProviderSourceDatasetVersion_FailedScenario_InvalidData_NullVersionIdThrowsArgumentNullException()
        {
            await _repository.DeleteProviderSourceDatasetVersion(null, DeletionType.PermanentDelete);
        }

        [TestMethod]
        public async Task DeleteProviderSourceDatasetVersion_EdgeCase_DataNotFound()
        {
            SetupTableProviderSourceDatasetVersion();

            await _repository.DeleteProviderSourceDatasetVersion("InvalidId", DeletionType.SoftDelete);

            _uowMock.Verify(x => x.CommitAsync(), Times.Never);

            var result = await _dbContext.ProviderSourceDatasetVersions.CountAsync();

            result.Should().Be(0);
        }
                
        #endregion

        #region Mock-Data

        private async void SetupTableProviderSourceDataset()
        {
            _uowMock.Setup(x => x.GenericRepository<EfcoreProviderSourceDataset>()).Returns(new GenericRepository<EfcoreProviderSourceDataset>(_dbContext));
        }

        private async void SetupTableProviderSourceDatasetVersion()
        {
            _uowMock.Setup(x => x.GenericRepository<EfcoreProviderSourceDatasetVersion>()).Returns(new GenericRepository<EfcoreProviderSourceDatasetVersion>(_dbContext));
        }

        private async void SetupProviderSourceDataset()
        {
           SetupTableProviderSourceDataset();

            _uowMock.Setup(x => x.GenericRepository<EfcoreProviderSourceDataset>())
                .Returns(new GenericRepository<EfcoreProviderSourceDataset>(_dbContext));

            _uowMock.Setup(x => x.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());
            
           var data = new List<EfcoreProviderSourceDataset>
            {
                new EfcoreProviderSourceDataset
                {
                    ProviderSourceDatasetId = "ver-001",
                    ProviderId = "provider-001",
                    SpecificationId = "spec-001",
                    DataDefinitionId = "def-001",
                    DatasetRelationshipSummaryId = "sum-001",
                    DataRelationshipSummaryName = "Patient Demographic Summary",
                    DataGranularity = "Individual",
                    DefinesScope = true,
                    IsDeleted = false,
                    DatasetRelationshipType = "Primary"
                },
                new EfcoreProviderSourceDataset
                {
                    ProviderSourceDatasetId = "dataset-002",
                    ProviderId = "provider-002",
                    SpecificationId = "spec-002",
                    DataDefinitionId = "def-002",
                    DatasetRelationshipSummaryId = "sum-002",
                    DataRelationshipSummaryName = "Claims History Data",
                    DataGranularity = "Transaction",
                    DefinesScope = false,
                    IsDeleted = false,
                    DatasetRelationshipType = "Secondary"
                },
                new EfcoreProviderSourceDataset
                {
                    ProviderSourceDatasetId = "dataset-003",
                    ProviderId = "provider-003",
                    SpecificationId = "spec-003",
                    DataDefinitionId = "def-003",
                    DatasetRelationshipSummaryId = "sum-003",
                    DataRelationshipSummaryName = "Provider Clinical Registry",
                    DataGranularity = "Aggregate",
                    DefinesScope = true,
                    IsDeleted = true,
                    DatasetRelationshipType = "Reference"
                },
                new EfcoreProviderSourceDataset
                {
                    ProviderSourceDatasetId = "dataset-005",
                    ProviderId = "provider-002",
                    SpecificationId = "spec-002",
                    DataDefinitionId = "def-002",
                    DatasetRelationshipSummaryId = "sum-002",
                    DataRelationshipSummaryName = "Claims History Data",
                    DataGranularity = "Transaction",
                    DefinesScope = true,
                    IsDeleted = false,
                    DatasetRelationshipType = "Secondary"
                }
            };
            _dbContext.ProviderSourceDatasets.AddRange(data);
            await _dbContext.SaveChangesAsync();

            foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        private async void SetupProviderSourceDatasetVersion()
        {
            SetupTableProviderSourceDatasetVersion();

            _uowMock.Setup(x => x.GenericRepository<EfcoreProviderSourceDatasetVersion>())
                .Returns(new GenericRepository<EfcoreProviderSourceDatasetVersion>(_dbContext));

            _uowMock.Setup(x => x.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());

            var data = new List<EfcoreProviderSourceDatasetVersion>
            {
                new EfcoreProviderSourceDatasetVersion
                {
                    ProviderSourceDatasetVersionId = "ver-001",
                    ProviderSourceDatasetId = "ver-001", 
                    DatasetId = "ds-100",
                    DatasetName = "Initial Import",
                    DatasetVersion = 1,
                    ProviderSourceDatasetRowData = "{'data': 'sample'}",
                    Version = 1,
                    AuthorId = "user-001",
                    AuthorName = "System Admin",
                    Comment = "Initial creation",
                    PublishStatus = "Approved",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    IsDeleted = false,
                    IsLatest = true
                },
                new EfcoreProviderSourceDatasetVersion
                {
                    ProviderSourceDatasetVersionId = "ver-002",
                    ProviderSourceDatasetId = "dataset-002", 
                    DatasetId = "ds-200",
                    DatasetName = "Updated Records",
                    DatasetVersion = 2,
                    ProviderSourceDatasetRowData = "{'data': 'updated'}",
                    Version = 1,
                    AuthorId = "user-002",
                    AuthorName = "Data Analyst",
                    Comment = "Corrected row values",
                    PublishStatus = "Draft",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    IsDeleted = false,
                    IsLatest = true
                },
                new EfcoreProviderSourceDatasetVersion
                {
                    ProviderSourceDatasetVersionId = "ver-003",
                    ProviderSourceDatasetId = "dataset-003",
                    DatasetId = "ds-300",
                    DatasetName = "Audit Version",
                    DatasetVersion = 1,
                    ProviderSourceDatasetRowData = "{'data': 'archived'}",
                    Version = 2,
                    AuthorId = "user-001",
                    AuthorName = "System Admin",
                    Comment = "Archiving record",
                    PublishStatus = "Archived",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    IsDeleted = true,
                    IsLatest = false
                }
            };
            _dbContext.ProviderSourceDatasetVersions.AddRange(data);
            await _dbContext.SaveChangesAsync();

            foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
        #endregion
    }
}