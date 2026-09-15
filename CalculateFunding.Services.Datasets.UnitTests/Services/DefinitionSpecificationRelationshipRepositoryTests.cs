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
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace CalculateFunding.Services.Datasets.UnitTests.Services
{
    [TestClass]
    public class DefinitionSpecificationRelationshipRepositoryTests
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected EntityModel.CfsDbContext _dbContext;
        protected DataSetsRepository _repository;

        public DefinitionSpecificationRelationshipRepositoryTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new EntityModel.CfsDbContext(new DbContextOptionsBuilder<EntityModel.CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            _repository = new DataSetsRepository(_uowMock.Object);
        }

        #region Get Methods

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDefinitionSpecificationRelationshipBy_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.GetDefinitionSpecificationRelationshipById(null);
        }

        [TestMethod]
        public async Task GetSpecificationById_WithValidData_ReturnsDefinitionSpecificationRelationship()                                                      
        {
            SetupData();

            var result = await _repository.GetDefinitionSpecificationRelationshipById("1");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(DefinitionSpecificationRelationship));
            Assert.AreEqual(3, result.Current.Version);
        }

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))]
        public async Task GetSpecificationById_WithoutData_ReturnsException()
        {
            var result = await _repository.GetDefinitionSpecificationRelationshipById("1");
        }

        [TestMethod]
        public async Task GetDefinitionSpecificationRelationshipsBySQLQuery_WithValidData_ReturnsDefinitionSpecificationRelationship()
        {
            SetupData();

            var result = await _repository.GetDefinitionSpecificationRelationshipsBySQLQuery(_=>_.IsLatest && !_.IsDeleted && _.SpecificationId=="test");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<DefinitionSpecificationRelationship>));
        }

        [TestMethod]
        public async Task GetDefinitionSpecificationRelationshipsBySpecificationId_WithValidData_ReturnsDefinitionSpecificationRelationship()
        {
            SetupData();

            var result = await _repository.GetDefinitionSpecificationRelationshipsBySpecificationId("test");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<DefinitionSpecificationRelationship>));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetRelationshipBySpecificationIdAndName_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.GetRelationshipBySpecificationIdAndName(null, null);
        }

        [TestMethod]
        public async Task GetRelationshipBySpecificationIdAndName_WithValidData_ReturnsDefinitionSpecificationRelationship()
        {
            SetupData();

            var result = await _repository.GetRelationshipBySpecificationIdAndName("test", "name");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(DefinitionSpecificationRelationship));
            Assert.AreEqual(3, result.Current.Version);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _repository.GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId(null);
        }

        [TestMethod]
        public async Task GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId_WithValidData_ReturnsSpecification()
        {
            SetupData();

            var result = await _repository.GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId("1");

            Assert.IsTrue(!result.IsNullOrEmpty());
        }

        [TestMethod]
        public async Task GetRelationshipSpecificationIdsForDatasetDefinitionId_WithValidData_ReturnsDefinitionSpecificationRelationship()
        {
            SetupData();

            var result = await _repository.GetRelationshipSpecificationIdsForDatasetDefinitionId("1");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<string>));
            Assert.AreEqual("test", result.FirstOrDefault());
        }

        #endregion

        #region Create and update

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveDefinitionSpecificationRelationship_WithNullData_ReturnsArgumentNullException()
        {
            var result = await _repository.SaveDefinitionSpecificationRelationship(null);
        }

        [TestMethod]
        public async Task SaveDefinitionSpecificationRelationship_WithValidData()
        {
            SetupData();

            DefinitionSpecificationRelationship definitionSpecificationRelationship = new DefinitionSpecificationRelationship()
            {
                DatasetId = "1",
                Name = "test",  
                Id = "1",
                Current = new DefinitionSpecificationRelationshipVersion()
                {
                    Name = "test",  
                    Date = DateTime.Now,
                    DatasetVersion = new DatasetRelationshipVersion()
                    {
                        Id = "1",
                        Version = 1
                    },
                    Version = 1,
                    IsSetAsProviderData = true,
                    LastUpdated = DateTime.Now,
                    Description = "test",
                    ConverterEnabled = true,
                    DatasetDefinition = new Common.Models.Reference()
                    {
                        Id = "1",
                        Name = "test",  
                    },
                    PublishedSpecificationConfiguration = null,
                    Specification = new Common.Models.Reference()
                    {
                        Id = "test",
                        Name = "test",
                    },
                    Author = new Common.Models.Reference() { Id = "" , Name = "test" }, 
                    RelationshipId = "1",
                }
            };

            var result = await _repository.SaveDefinitionSpecificationRelationship(definitionSpecificationRelationship);

            Assert.IsNotNull(result);
            Assert.IsTrue(result == HttpStatusCode.OK);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task UpdateDefinitionSpecificationRelationship_WithNullData_ReturnsArgumentNullException()
        {
            var result = await _repository.UpdateDefinitionSpecificationRelationship(null);
        }

        [TestMethod]
        public async Task UpdateDefinitionSpecificationRelationship_WithValidData()
        {
            SetupData();

            DefinitionSpecificationRelationship definitionSpecificationRelationship = new DefinitionSpecificationRelationship()
            {
                DatasetId = "1",
                Name = "test",
                Id = "1",
                Current = new DefinitionSpecificationRelationshipVersion()
                {
                    Name = "test",
                    Date = DateTime.Now,
                    DatasetVersion = new DatasetRelationshipVersion()
                    {
                        Id = "1",
                        Version = 1
                    },
                    Version = 1,
                    IsSetAsProviderData = true,
                    LastUpdated = DateTime.Now,
                    Description = "test",
                    ConverterEnabled = true,
                    DatasetDefinition = new Common.Models.Reference()
                    {
                        Id = "1",
                        Name = "test",
                    },
                    PublishedSpecificationConfiguration = null,
                    Specification = new Common.Models.Reference()
                    {
                        Id = "test",
                        Name = "test",
                    },
                    Author = new Common.Models.Reference() { Id = "", Name = "test" },
                    RelationshipId = "1",
                }
            };

            DetachAllDBEntries();

            var result = await _repository.UpdateDefinitionSpecificationRelationship(definitionSpecificationRelationship);

            Assert.IsNotNull(result);
            Assert.IsTrue(result == HttpStatusCode.OK);
        }

        #endregion

        #region Delete


        [TestMethod]
        public async Task DeleteDefinitionSpecificationRelationshipBySpecificationId_WithValidData_SoftDelete()
        {
            SetupData();

            DetachAllDBEntries();

            await _repository.DeleteDefinitionSpecificationRelationshipBySpecificationId("test", Models.Messages.DeletionType.SoftDelete);

            Assert.IsTrue(_dbContext.DefinitionSpecificationRelationships.FirstOrDefault().IsDeleted);

            Assert.IsNull(_dbContext.DefinitionSpecificationRelationships.Where(_ => _.IsDeleted).FirstOrDefault());
        }

        [TestMethod]
        public async Task DeleteDefinitionSpecificationRelationshipBySpecificationId_WithValidData_HardDelete()
        {
            SetupData();

            DetachAllDBEntries();

            await _repository.DeleteDefinitionSpecificationRelationshipBySpecificationId("test", Models.Messages.DeletionType.PermanentDelete);

            //Support for hard delete has been removed
            Assert.IsNotNull(_dbContext.DefinitionSpecificationRelationships.Where(_ => !_.IsDeleted).FirstOrDefault());
        }

        #endregion


        #region Private methods

        private async void SetupData()
        {
            SetupTables();

            PublishedSpecificationConfiguration publishedFundingConfiguration = new PublishedSpecificationConfiguration()
            {
                FundingLines = new List<PublishedSpecificationItem>() { },
                Calculations = new List<PublishedSpecificationItem>() { },
                FundingStreamId = "",
                FundingPeriodId = "",
                SpecificationId = "test",
                IncludeCarryForward = false
            };
            
            for (int i = 1; i <= 3; i++)
            {
                var datasetSpecRelationshipEntry = new EntityModel.DatasetSpecificationRelationship()
                {
                    IsDeleted = false,
                    IsLatest = i == 3,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    PublishStatus = PublishStatus.Approved.ToString(),
                    Version = i,
                    RelationshipType = DatasetRelationshipType.FDS.ToString(),
                    UsedInDataAggregations = false,
                    ConverterEnabled = false,
                    IsSetAsProviderData = false,
                    Description = "test",
                    SpecificationId = "test",
                    Name = "name",
                    DatasetSpecificationRelationshipId = "1",
                    DatasetSpecificationRelationshipVersionId = i.ToString(),
                    AuthorId = "id",
                    AuthorName = "name",
                    DatasetDefinitionId = "1",
                    DatasetDefinitionName = "1-name",
                    PublishedSpecificationConfiguration = JsonConvert.SerializeObject(publishedFundingConfiguration)
                };
                _dbContext.DatasetSpecificationRelationships.Add(datasetSpecRelationshipEntry);
            }

            var specTableEntry = new EntityModel.Specification()
            {
                ForceUpdateOnNextRefresh = true,
                SpecificationId = "test",
                IsDeleted = false,
                SpecificationName = "Test Name",
                FundingPeriodId = 1,
                FundingStreamId = 1,
                IsSelectedForFunding = true,
            };
            _dbContext.Specifications.Add(specTableEntry);

            var specVersionTableEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                IsLatest = true,
                SpecificationVersionId = "v3",
                SpecificationId = "test",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Updated",
                ProviderSource = "FDZ"
            };
            _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            var definitionSpecificationRelationshipTableEntry = new EntityModel.DefinitionSpecificationRelationship()
            {
                DataDefinitionRelationshipId = "1",
                Id = 1,
                IsDeleted = false,
                SpecificationVersionId= "v3",

            };

            _dbContext.DefinitionSpecificationRelationships.Add(definitionSpecificationRelationshipTableEntry);

            await _dbContext.SaveChangesAsync();

        }
        private async void SetupTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DatasetSpecificationRelationship>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Specification>()).Returns(new GenericRepository<EntityModel.Specification>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.SpecificationVersion>()).Returns(new GenericRepository<EntityModel.SpecificationVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DefinitionSpecificationRelationship>(_dbContext));

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
