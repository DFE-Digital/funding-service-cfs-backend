using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Common.Models.Versioning;

namespace CalculateFunding.Services.Datasets.UnitTests
{
    [TestClass]
    public class ProviderSourceDatasetsRepositoryTests
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected CfsDbContext _dbContext;
        protected ProviderSourceDatasetsRepository _providerSourceDatasetsRepository;
        public ProviderSourceDatasetsRepositoryTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
            _providerSourceDatasetsRepository = new ProviderSourceDatasetsRepository(_uowMock.Object);
        }

        #region Private Methods
        private void SetUpProviderSourceDatasetTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.ProviderSourceDataset>()).Returns(new GenericRepository<EntityModel.ProviderSourceDataset>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.ProviderSourceDatasetVersion>()).Returns(new GenericRepository<EntityModel.ProviderSourceDatasetVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DatasetSpecificationRelationship>(_dbContext));
        }

        #region Methods
        [TestMethod]
        public async Task GetCurrentProviderSourceDatasets_WithInValidData_ReturnsNull()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();

            var results = await _providerSourceDatasetsRepository.GetCurrentProviderSourceDatasets("1780709","test");
            
            Assert.IsTrue(results.IsNullOrEmpty());

        }

        [TestMethod]
        public async Task GetCurrentProviderSourceDatasets_WithValidData_ReturnsProviderSourceDatasets()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();

            var results = await _providerSourceDatasetsRepository.GetCurrentProviderSourceDatasets("1", "114af795-b2e1-45e5-88c9-83f46b8c0f6a");
           
            Assert.IsNotNull(results);        
        }

        [TestMethod]
        public async Task DeleteProviderSourceDatasets_WithValidData_SoftDelete()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();

            DetachAllDBEntries();

            await _providerSourceDatasetsRepository.DeleteProviderSourceDataset("114af795-b2e1-45e5-88c9-83f46b8c0f6a", Models.Messages.DeletionType.SoftDelete);

            //Support for hard delete has been removed
            Assert.IsTrue(_dbContext.ProviderSourceDatasets.FirstOrDefault().IsDeleted);
        }

        [TestMethod]
        public async Task DeleteProviderSourceDatasetVersion_WithValidData_SoftDelete()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();

            DetachAllDBEntries();

            await _providerSourceDatasetsRepository.DeleteProviderSourceDatasetVersion("114af795-b2e1-45e5-88c9-83f46b8c0f6a", Models.Messages.DeletionType.SoftDelete);

            //Support for hard delete has been removed
            Assert.IsTrue(_dbContext.ProviderSourceDatasetVersions.FirstOrDefault().IsDeleted);
        }

        [TestMethod]
        public async Task DeleteProviderSourceDatasets_WithValidData_PermanentDelete()
        {
            SetUpProviderSourceDatasetTables();
            SetUpProviderSourceDatasetTableData();

            DetachAllDBEntries();

            await _providerSourceDatasetsRepository.DeleteProviderSourceDataset("114af795-b2e1-45e5-88c9-83f46b8c0f6a", Models.Messages.DeletionType.PermanentDelete);
            _dbContext.SaveChanges();
            var permanentDeleteData = _dbContext.ProviderSourceDatasets.FirstOrDefault(x => x.ProviderSourceDatasetId == "325-1");
            Assert.IsNull(permanentDeleteData);

        }
        #endregion

        #region Private Methods
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
           
            for (int i = 1; i <= 1; i++)
            {
                var providerSourceDatasetData = new EntityModel.ProviderSourceDataset()
                {
                    ProviderSourceDatasetId = "325-" + i,
                    ProviderId = "1000360",
                    DataDefinitionId = "340",
                    DatasetRelationshipSummaryId = "114af795-b2e1-45e5-88c9-83f46b8c0f6a",
                    DataRelationshipSummaryName = "PIT-" + i,
                    DataGranularity = i == 1 ?DataGranularity.MultipleRowsPerProvider.ToString() : DataGranularity.SingleRowPerProvider.ToString(),
                    DefinesScope = false,
                    DatasetRelationshipType = DatasetRelationshipType.FDS.ToString(),
                    SpecificationId = i.ToString(),
                    IsDeleted = false,
                };
                _dbContext.ProviderSourceDatasets.Add(providerSourceDatasetData);

                var providerSourceDatasetVersionTableEntry = new EntityModel.ProviderSourceDatasetVersion()
                {
                    ProviderSourceDatasetVersionId = "V-" + i,
                    ProviderSourceDatasetId = "325-" + i,
                    DatasetId = i.ToString(),
                    DatasetVersion =  1 + i,
                    ProviderSourceDatasetRowData = JsonConvert.SerializeObject(providerSourceDatasetVersion.Rows),
                    IsDeleted = false,
                    IsLatest = true,
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
        #endregion

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
