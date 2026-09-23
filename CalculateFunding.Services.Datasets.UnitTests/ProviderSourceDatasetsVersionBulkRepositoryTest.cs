using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Common.Models.Versioning;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Results.Models;
using Model = CalculateFunding.Models.Datasets;
using DataGranularity = CalculateFunding.Models.Datasets.Schema.DataGranularity;
using CalculateFunding.Common.Models;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.Net;

namespace CalculateFunding.Services.Datasets.UnitTests
{
    [TestClass]
    public class ProviderSourceDatasetsVersionBulkRepositoryTest
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected CfsDbContext _dbContext;
        protected ProviderSourceDatasetsVersionBulkRepository<Model.ProviderSourceDatasetVersion> _repository;
        public ProviderSourceDatasetsVersionBulkRepositoryTest()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
            _repository = new ProviderSourceDatasetsVersionBulkRepository<Model.ProviderSourceDatasetVersion>(_uowMock.Object);
        }

        #region Private Methods
        private void SetUpProviderSourceDatasetTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.ProviderSourceDataset>()).Returns(new GenericRepository<EntityModel.ProviderSourceDataset>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.ProviderSourceDatasetVersion>()).Returns(new GenericRepository<EntityModel.ProviderSourceDatasetVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DatasetSpecificationRelationship>(_dbContext));
        }

        private async void SetUpProviderSourceDatasetTableData()
        {
            SetUpProviderSourceDatasetTables();

            List<Dictionary<string, object>> providerSourceDatasetRowData = new List<Dictionary<string, object>>(){
                new Dictionary<string, object>() {
                    {"7726", 10082366},
                    {"7727", "Contract for Services"}
                },
                new Dictionary<string, object>() {
                    {"7728", 10082366},
                    {"7729", "Foo"}
                },
            };
            CalculateFunding.Models.Datasets.ProviderSourceDatasetVersion providerSourceDatasetVersion = new CalculateFunding.Models.Datasets.ProviderSourceDatasetVersion()
            {
                Rows = providerSourceDatasetRowData
            };

            for (int i = 1; i <= 2; i++)
            {
                var providerSourceDatasetData = new EntityModel.ProviderSourceDataset()
                {
                    ProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_" + i,
                    ProviderId = "1000360",
                    DataDefinitionId = "340",
                    DatasetRelationshipSummaryId = "114af795-b2e1-45e5-88c9-83f46b8c0f6a" + i,
                    DataRelationshipSummaryName = "PIT-" + i,
                    DataGranularity = i == 1 ? DataGranularity.MultipleRowsPerProvider.ToString() : DataGranularity.SingleRowPerProvider.ToString(),
                    DefinesScope = false,
                    DatasetRelationshipType = DatasetRelationshipType.FDS.ToString(),
                    SpecificationId = i.ToString(),
                    IsDeleted = false,
                };
                _dbContext.ProviderSourceDatasets.Add(providerSourceDatasetData);

                var providerSourceDatasetVersionTableEntry = new EntityModel.ProviderSourceDatasetVersion()
                {
                    ProviderSourceDatasetVersionId = "V-" + i,
                    ProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_" + i,
                    DatasetId = i.ToString(),
                    DatasetVersion = 1 + i,
                    ProviderSourceDatasetRowData = JsonConvert.SerializeObject(providerSourceDatasetVersion.Rows),
                    IsDeleted = false,
                    IsLatest = (i == 1),
                    AuthorId = i.ToString(),
                    AuthorName = i.ToString(),
                    PublishStatus = PublishStatus.Approved.ToString(),
                    Comment = i.ToString(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Date = DateTime.Now,
                    Version = i,
                };
                _dbContext.ProviderSourceDatasetVersions.Add(providerSourceDatasetVersionTableEntry);
                var datasetSpecificationRelationshipTableEntry = new EntityModel.DatasetSpecificationRelationship()
                {
                    DatasetSpecificationRelationshipVersionId = Guid.NewGuid().ToString(),
                    DatasetId = i.ToString(),
                    Name = "FDS",
                    SpecificationId = i.ToString(),
                    Version = i,
                    PublishedSpecificationId = i.ToString(),
                    IsSetAsProviderData = true,
                    IsLatest = true,
                    IsDeleted = false,
                    Description = "TestData",
                    DatasetSpecificationRelationshipId = Guid.NewGuid().ToString(),
                    PublishStatus = PublishStatus.Approved.ToString(),
                    RelationshipType = "FDS",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    DatasetDefinitionId = i.ToString(),

                };
                _dbContext.DatasetSpecificationRelationships.Add(datasetSpecificationRelationshipTableEntry);


            }
            await _dbContext.SaveChangesAsync();
        }

        private void DetachAllDBEntries()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
        }
        #endregion

        #region methods
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetNextVersionNumber_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.GetNextVersionNumber(null, 0, null, true);
        }

        [TestMethod]
        public async Task GetNextVersionNumber_WithValidData_ReturnsNextVersionFromProvidedVersion()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            DetachAllDBEntries();
            Model.ProviderSourceDatasetVersion providerSourceDatasetVersion = new Model.ProviderSourceDatasetVersion()
            {
                ProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_1"
            };
            var result = await _repository.GetNextVersionNumber(providerSourceDatasetVersion, 3, null, true);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(int));
            Assert.AreEqual(4, result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CreateVersion_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.CreateVersion(null, null, null, true);
        }

        [TestMethod]
        public async Task CreateVersion_WithValidDatasetId_ReturnsDatasetVersionList()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            DetachAllDBEntries();
            Model.ProviderSourceDatasetVersion providerSourceDatasetVersion = new Model.ProviderSourceDatasetVersion()
            {
                ProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_1",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft
            };
            var result = await _repository.CreateVersion(providerSourceDatasetVersion, providerSourceDatasetVersion, null, false);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Model.ProviderSourceDatasetVersion));
            Assert.AreEqual(2, result.Version);
        }

        [TestMethod]
        public async Task CreateVersion_WithMissingDatasetId_ReturnsDatasetVersionList()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            DetachAllDBEntries();
            Model.ProviderSourceDatasetVersion providerSourceDatasetVersion = new Model.ProviderSourceDatasetVersion()
            {
                ProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_2",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft
            };
            var result = await _repository.CreateVersion(providerSourceDatasetVersion, providerSourceDatasetVersion, null, true);

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Model.ProviderSourceDatasetVersion));
            Assert.AreEqual(1, result.Version);
        }

        [TestMethod]
        public async Task SaveVersions_WithValidData_ReturnsDatasetVersionList()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            List<Dictionary<string, object>> providerSourceDatasetRowData = new List<Dictionary<string, object>>(){
                new Dictionary<string, object>() {
                    {"7726", 10082366},
                    {"7727", "Contract for Services"}
                },
                new Dictionary<string, object>() {
                    {"7728", 10082366},
                    {"7729", "Foo"}
                },
            };
            Model.ProviderSourceDatasetVersion providerSourceDatasetVersion = new()
            {               
                ProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_3",
                Author = new Common.Models.Reference()
                {
                    Name = "Author",
                    Id = "unknown"
                },
                Comment = "test",
                Checksum = "test",
                Dataset = new Models.VersionReference("1", "DataSetName", 1),
                Date = DateTimeOffset.Now,
                ProviderId = "1000360_1",
                PublishStatus = (Models.Versioning.PublishStatus)PublishStatus.Draft,
                Version = 1,
                Rows = providerSourceDatasetRowData
            };

            DetachAllDBEntries();

            var result = await _repository.SaveVersions(new List<Model.ProviderSourceDatasetVersion>() { providerSourceDatasetVersion });

            Assert.IsNotNull(result);
            Assert.IsTrue(result == HttpStatusCode.Created);
            #endregion


        }
    }
}
