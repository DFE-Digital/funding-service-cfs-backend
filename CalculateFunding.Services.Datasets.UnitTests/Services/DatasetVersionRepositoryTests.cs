using CalculateFunding.Common.EfCore.UnitOfWork;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.ApiClient.Models;
using System.Linq;
using CalculateFunding.Models.Datasets;
using Model = CalculateFunding.Models.Datasets;
using System.Collections.Generic;
using System.Net;

namespace CalculateFunding.Services.Datasets.UnitTests.Services
{
    [TestClass]
    public class DatasetVersionRepositoryTests
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected EntityModel.CfsDbContext _dbContext;
        protected DatasetVersionsRepository<DatasetVersion> _repository;

        public DatasetVersionRepositoryTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new EntityModel.CfsDbContext(new DbContextOptionsBuilder<EntityModel.CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            _repository = new DatasetVersionsRepository<DatasetVersion>(_uowMock.Object);
        }

        #region Get methods
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetNextVersionNumber_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.GetNextVersionNumber(null, 0, null, true);
        }

        [TestMethod]
        public async Task GetNextVersionNumber_WithValidData_ReturnsNextVersion()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1"
            };
            var result = await _repository.GetNextVersionNumber(datasetNewVersion, 0, null, false);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(5, result);
        }

        [TestMethod]
        public async Task GetNextVersionNumber_WithValidData_ReturnsNextVersionFromProvidedVersion()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1"
            };
            var result = await _repository.GetNextVersionNumber(datasetNewVersion, 3, null, true);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(4, result);
        }

        [TestMethod]
        public async Task GetVersion_WithValidData_ReturnsDatasetVersion()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1"
            };
            var result = await _repository.GetVersion("1", 2);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(DatasetVersion));
            Assert.AreEqual(2, result.Version);
        }

        [TestMethod]
        public async Task GetVersions_WithValidData_ReturnsDatasetVersionList()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1"
            };
            var result = await _repository.GetVersions("1", null);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<DatasetVersion>));
            Assert.AreEqual(4, result.Count());
        }

        [TestMethod]
        public async Task GetVersions_WithMissingVersionId_ReturnsDatasetVersionList()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1"
            };
            var result = await _repository.GetVersions("6", null);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<DatasetVersion>));
            Assert.AreEqual(0, result.Count());
        }


        #endregion

        #region Create and Save

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CreateVersion_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.CreateVersion(null, null, null, true);
        }

        [TestMethod]
        public async Task CreateVersion_WithValidDatasetId_ReturnsDatasetVersionList()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft
            };
            var result = await _repository.CreateVersion(datasetNewVersion, datasetNewVersion, null, false);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(DatasetVersion));
            Assert.AreEqual(5, result.Version);
        }

        [TestMethod]
        public async Task CreateVersion_WithMissingDatasetId_ReturnsDatasetVersionList()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "2",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft
            };
            var result = await _repository.CreateVersion(datasetNewVersion, datasetNewVersion, null, true);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(DatasetVersion));
            Assert.AreEqual(1, result.Version);
        }

        [TestMethod]
        public async Task SaveVersion_WithValidData_ReturnsDatasetVersionList()
        {
            SetupData();
            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft,
                BlobName = "blobName",
                FundingStream = new Common.Models.Reference()
                {
                    Id = "1619",
                    Name = "1619"
                },
                Date = DateTime.UtcNow,
                AmendedRowCount = 1,
                Comment = "Comment",
                Description = "Description",
                NewRowCount = 1,
                ProviderVersionId = "1",
                Version = 5,
                RowCount = 1,
                UploadedBlobFilePath = "path",
                Author = new Common.Models.Reference()
                {
                    Name = "Author",
                    Id = "unknown"
                },
                ChangeType = Model.DatasetChangeType.ConverterWizard,
            };

            DetachAllDBEntries();

            var result = await _repository.SaveVersion(datasetNewVersion);

            Assert.IsNotNull(result);
            Assert.IsTrue(result == HttpStatusCode.Created);
        }


        [TestMethod]
        public async Task SaveVersions_WithValidData_ReturnsDatasetVersionList()
        {
            SetupData();

            List<Model.DatasetVersion> datasetVersions = new List<DatasetVersion>();

            Model.DatasetVersion datasetNewVersion = new Model.DatasetVersion()
            {
                DatasetId = "1",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft,
                BlobName = "blobName",
                FundingStream = new Common.Models.Reference()
                {
                    Id = "1619",
                    Name = "1619"
                },
                Date = DateTimeOffset.Now.ToLocalTime(),
                AmendedRowCount = 1,
                Comment = "Comment",
                Description = "Description",
                NewRowCount = 1,
                ProviderVersionId = "1",
                Version = 5,
                RowCount = 1,
                UploadedBlobFilePath = "path",
                Author = new Common.Models.Reference()
                {
                    Name = "Author",
                    Id = "unknown"
                },
                ChangeType = Model.DatasetChangeType.ConverterWizard,
            };
            datasetVersions.Add(datasetNewVersion);

            Model.DatasetVersion datasetlatestVersion = new Model.DatasetVersion()
            {
                DatasetId = "2",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft,
                BlobName = "blobName",
                FundingStream = new Common.Models.Reference()
                {
                    Id = "1619",
                    Name = "1619"
                },
                Date = DateTime.UtcNow,
                AmendedRowCount = 1,
                Comment = "Comment",
                Description = "Description",
                NewRowCount = 1,
                ProviderVersionId = "1",
                Version = 1,
                RowCount = 1,
                UploadedBlobFilePath = "path",
                Author = new Common.Models.Reference()
                {
                    Name = "Author",
                    Id = "unknown"
                },
                ChangeType = Model.DatasetChangeType.ConverterWizard,
            };
            datasetVersions.Add(datasetlatestVersion);

            DetachAllDBEntries();

            await _repository.SaveVersions(datasetVersions, 0);

            Assert.IsTrue(!_dbContext.DatasetVersions.LastOrDefaultAsync().Result.IsLatest);
        }
        #endregion

        #region Private methods

        private async void SetupData()
        {
            SetupTables();
            var datasetTableEntry = new EntityModel.Dataset()
            {
                DatasetId = "1",
                DatasetSpecificationRelationshipId = "1",
                DefinitionId = "1",
                DefinitionVersion = 1,
                DefinitionName = "dname",
                IsDeleted = false,
                Name = "Name",  
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _dbContext.Datasets.Add(datasetTableEntry);

            for(int i=0; i<3; i++)
            {
                var versionTableEntry = new EntityModel.DatasetVersion()
                {
                    BlobName = "blobName",
                    DatasetId = "1",
                    DatasetVersionId = "v" + i,
                    IsLatest = false,
                    IsDeleted = false,
                    Date = DateTime.UtcNow,
                    AmendedRowCount = 1,
                    AuthorId = "1",
                    AuthorName = "Name",
                    ChangeType = "1",
                    Comment = "Comment",
                    CreatedAt = DateTime.Now,
                    Description = "Description",
                    NewRowCount = 1,
                    ProviderVersionId = "1",
                    PublishStatus = PublishStatus.Approved.ToString(),
                    UpdatedAt = DateTime.UtcNow,
                    Version = i,
                    RowCount = 1,
                    UploadedBlobFilePath = "path",
                    FundingStreamId = 1
                };
                _dbContext.DatasetVersions.Add(versionTableEntry);
            }

            var datasetVersionTableEntry = new EntityModel.DatasetVersion()
            {
                BlobName = "blobName",
                DatasetId = "1",
                DatasetVersionId = "v4",
                IsLatest = true,
                IsDeleted = false,
                Date = DateTime.UtcNow,
                AmendedRowCount = 1,
                AuthorId = "1",
                AuthorName = "Name",
                ChangeType = "1",
                Comment = "Comment",
                CreatedAt = DateTime.Now,
                Description = "Description",
                NewRowCount = 1,
                ProviderVersionId = "1",
                PublishStatus = PublishStatus.Approved.ToString(),
                UpdatedAt = DateTime.UtcNow,
                Version = 4,
                RowCount = 1,
                UploadedBlobFilePath = "path",
                FundingStreamId = 1
            };
            _dbContext.DatasetVersions.Add(datasetVersionTableEntry);

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "1619",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

            await _dbContext.SaveChangesAsync();

        }

        private async void SetupTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Dataset>()).Returns(new GenericRepository<EntityModel.Dataset>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetVersion>()).Returns(new GenericRepository<EntityModel.DatasetVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));

        }

        private void DetachAllDBEntries()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
        }

        #endregion
    }
}
