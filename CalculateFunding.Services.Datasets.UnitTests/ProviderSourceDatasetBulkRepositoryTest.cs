using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Moq;
using System;
using System.Collections.Generic;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Newtonsoft.Json;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Common.Models.Versioning;
using System.Threading.Tasks;
using CalculateFunding.Common.Models;
using System.Linq;


namespace CalculateFunding.Services.Datasets.UnitTests
{
    [TestClass]
    public class ProviderSourceDatasetBulkRepositoryTest
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected CfsDbContext _dbContext;
        protected ProviderSourceDatasetBulkRepository _providerSourceDatasetBulkRepository;
        public ProviderSourceDatasetBulkRepositoryTest()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
            _providerSourceDatasetBulkRepository = new ProviderSourceDatasetBulkRepository(_uowMock.Object);
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

        #region Method

        [TestMethod]
        public async Task InsertCurrentProviderSourceDatasets_WithValidData()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            DetachAllDBEntries();
            string expectedProviderSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_3";
            List<CalculateFunding.Models.Datasets.ProviderSourceDataset> providerSourceDatasets = new()
            {
                  new Models.Datasets.ProviderSourceDataset
                  {
                           SpecificationId = "1",
                           ProviderId  = "1000360_3",
                           DataDefinitionId = "340",
                           DefinesScope = false,
                           DataRelationship = new Reference()
                           {
                               Id = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                               Name = "Name",
                           },
                            DatasetRelationshipSummary = new Reference()
                            {
                                Id = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                                Name = "Name1",
                            },
                           DataGranularity =  DataGranularity.SingleRowPerProvider,
                           DatasetRelationshipType = DatasetRelationshipType.FDS

                  },

            };
            
         

            await _providerSourceDatasetBulkRepository.UpdateCurrentProviderSourceDatasets(providerSourceDatasets);
            _dbContext.SaveChanges();
            var ProviderSourceDatasetId = _dbContext.ProviderSourceDatasets.FirstOrDefault(x => x.ProviderSourceDatasetId == expectedProviderSourceDatasetId).ProviderSourceDatasetId;

            Assert.AreEqual(expectedProviderSourceDatasetId, ProviderSourceDatasetId);
        }

        [TestMethod]
        public async Task UpdateCurrentProviderSourceDatasets_WithValidData()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            DetachAllDBEntries();
            bool expectedDefinesScope = true;
            List<CalculateFunding.Models.Datasets.ProviderSourceDataset> providerSourceDatasets = new()
            {
                  new Models.Datasets.ProviderSourceDataset
                  {   
                           SpecificationId = "1",
                           ProviderId  = "1000360_2",
                           DataDefinitionId = "340",
                           DefinesScope = true,
                           DataRelationship = new Reference()
                           {
                               Id = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                               Name = "Name",
                           },
                            DatasetRelationshipSummary = new Reference()
                            {
                                Id = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                                Name = "Name1",
                            }
                  },

            };



            await _providerSourceDatasetBulkRepository.UpdateCurrentProviderSourceDatasets(providerSourceDatasets);
            _dbContext.SaveChanges();
            var definesScope = _dbContext.ProviderSourceDatasets.FirstOrDefault(x => x.DefinesScope == expectedDefinesScope).DefinesScope;

            Assert.AreEqual(definesScope, expectedDefinesScope);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task UpdateCurrentProviderSourceDatasets_WithNullData_ThrowArgumentException()
        {

            await _providerSourceDatasetBulkRepository.UpdateCurrentProviderSourceDatasets(null);

        }

        [TestMethod]
        public async Task DeleteCurrentProviderSourceDatasets_WithValidData()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();
            DetachAllDBEntries();
            string providerSourceDatasetId = "1_114af795-b2e1-45e5-88c9-83f46b8c0f6a_1000360_2";
            List<CalculateFunding.Models.Datasets.ProviderSourceDataset> providerSourceDatasets = new()
            {
                  new Models.Datasets.ProviderSourceDataset
                  {
                           SpecificationId = "1",
                           ProviderId  = "1000360_2",
                           DataDefinitionId = "340",
                           DefinesScope = false,
                           DataRelationship = new Reference()
                           {
                               Id = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                               Name = "Name",
                           },
                            DatasetRelationshipSummary = new Reference()
                            {
                                Id = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                                Name = "Name1",
                            },
                           DataGranularity =  DataGranularity.SingleRowPerProvider,
                           DatasetRelationshipType = DatasetRelationshipType.FDS

                  },

            };
            await _providerSourceDatasetBulkRepository.DeleteCurrentProviderSourceDatasets(providerSourceDatasets);
            _dbContext.SaveChanges();
            var isDeleted = _dbContext.ProviderSourceDatasets.FirstOrDefault(x => x.ProviderSourceDatasetId == providerSourceDatasetId).IsDeleted;
            Assert.AreEqual(true, isDeleted);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DeleteCurrentProviderSourceDatasets_WithNullData_ThrowArgumentException()
        {

            await _providerSourceDatasetBulkRepository.DeleteCurrentProviderSourceDatasets(null);

        }

        #endregion  
    }
}
