using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Calcs.ObsoleteItems;
using CalculateFunding.Models.Messages;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CalculateFunding.Services.Calcs.UnitTests
{
    [TestClass]
    public class CalculationsRepositoryTests
    {
        private EntityModel.CfsDbContext _dbContext;
        private IUnitOfWork _unitOfWork;
        private CalculationsRepository _repository;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<EntityModel.CfsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new EntityModel.CfsDbContext(options);
            _unitOfWork = new UnitOfWork(_dbContext);
            _repository = new CalculationsRepository(_unitOfWork);

            SetupData();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }

        private void SetupData()
        {
            // Add Funding Streams
            _dbContext.FundingStreams.Add(new EntityModel.FundingStream
            {
                FundingStreamId = 1,
                FundingStreamCode = "FS1",
                FundingStreamName = "Test Funding Stream 1",
                ShortFundingStreamName = "TestFS1"
            });

            _dbContext.FundingStreams.Add(new EntityModel.FundingStream
            {
                FundingStreamId = 2,
                FundingStreamCode = "FS2",
                FundingStreamName = "Test Funding Stream 2",
                ShortFundingStreamName = "TestFS2"
            });

            // Add Calculations
            _dbContext.Calculations.Add(new EntityModel.Calculation
            {
                CalculationId = "calc1",
                SpecificationId = "spec1",
                FundingStreamId = 1,
                IsDeleted = false
            });

            _dbContext.Calculations.Add(new EntityModel.Calculation
            {
                CalculationId = "calc2",
                SpecificationId = "spec1",
                FundingStreamId = 1,
                IsDeleted = false
            });

            // Add Calculation Versions
            _dbContext.CalculationVersions.Add(new EntityModel.CalculationVersion
            {
                CalculationId = "calc1",
                CalculationVersionId = "calc1_version_1",
                PublishStatus = "Draft",
                AuthorId = "TestAuth1",
                AuthorName = "Test Author 1",
                CalculationType = "Template",
                Name = "Test Calculation",
                Namespace = "Template",
                SourceCode = "Return 0",
                SourceCodeName = "Test Calculation SourceCode",
                ValueType = "Number",
                IsLatest = true,
                IsDeleted = false
            });

            _dbContext.CalculationVersions.Add(new EntityModel.CalculationVersion
            {
                CalculationId = "calc2",
                CalculationVersionId = "calc2_version_1",
                PublishStatus = "Approved",
                AuthorId = "TestAuth1",
                AuthorName = "Test Author 1",
                CalculationType = "Template",
                Name = "Test Calculation",
                Namespace = "Template",
                SourceCode = "Return 0",
                SourceCodeName = "Test Calculation SourceCode",
                ValueType = "Number",
                IsLatest = true,
                IsDeleted = false
            });

            // Add Obsolete Item
            _dbContext.ObsoleteItems.Add(new EntityModel.ObsoleteItem
            {
                ObsoleteItemId = "obsolete1",
                SpecificationId = "spec1",
                DatasetRelationshipName = "TestRelationship",
                FundingStreamId = 1,
                DatasetDataType = "DateTime",
                CodeReference = "Return 0",
                ItemType = "DatasetField",
                IsReleasedData = false,
                IsDeleted = false
            });

            //Add Obsolete Item Calcs
            _dbContext.ObsoleteItemCalcs.Add(new EntityModel.ObsoleteItemCalc
            {
                ObsoleteItemId = "obsolete1",
                CalculationId = "calc1"
            });

            _dbContext.SaveChanges();
        }

        [TestMethod]
        public async Task CreateDraftCalculation_WithValidData_ReturnsCreatedStatusCode()
        {
            var calculation = new Calculation
            {
                Id = "calc3",
                SpecificationId = "spec1",
                FundingStreamId = "FS1"
            };

            var result = await _repository.CreateDraftCalculation(calculation);

            await _dbContext.SaveChangesAsync();

            Assert.AreEqual(HttpStatusCode.Created, result);

            var calculationInDb = _dbContext.Calculations.SingleOrDefault(c => c.CalculationId == "calc3");
            Assert.IsNotNull(calculationInDb);
            Assert.AreEqual("spec1", calculationInDb.SpecificationId);
            Assert.AreEqual(1, calculationInDb.FundingStreamId);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CreateDraftCalculation_WithNullCalculation_ThrowsArgumentNullException()
        {
            await _repository.CreateDraftCalculation(null);
        }

        [TestMethod]
        public async Task DeleteCalculationsBySpecificationId_WithSoftDelete_PerformsSoftDelete()
        {
            DetachAllDBEntries();

            await _repository.DeleteCalculationsBySpecificationId("spec1", DeletionType.SoftDelete);

            await _dbContext.SaveChangesAsync();

            var calculations = _dbContext.Calculations.Where(c => c.SpecificationId == "spec1").ToList();
            Assert.IsTrue(calculations.All(c => c.IsDeleted));
        }

        [TestMethod]
        public async Task DeleteCalculationsBySpecificationId_WithHardDelete_PerformsHardDelete()
        {
            DetachAllDBEntries();

            await _repository.DeleteCalculationsBySpecificationId("spec1", DeletionType.PermanentDelete);

            await _dbContext.SaveChangesAsync();

            var calculations = _dbContext.Calculations.Where(c => c.SpecificationId == "spec1").ToList();
            Assert.AreEqual(0, calculations.Count);
        }

        [TestMethod]
        public async Task GetCalculationsBySpecificationId_WithValidSpecificationId_ReturnsCalculations()
        {
            var calculations = await _repository.GetCalculationsBySpecificationId("spec1");

            Assert.IsNotNull(calculations);
            Assert.AreEqual(2, calculations.Count());
            Assert.IsTrue(calculations.Any(c => c.Id == "calc1"));
            Assert.IsTrue(calculations.Any(c => c.Id == "calc2"));
        }

        [TestMethod]
        public async Task GetCalculationById_WithValidId_ReturnsCalculation()
        {
            var calculation = await _repository.GetCalculationById("calc1");

            Assert.IsNotNull(calculation);
            Assert.AreEqual("calc1", calculation.Id);
            Assert.AreEqual("spec1", calculation.SpecificationId);
            Assert.AreEqual("FS1", calculation.FundingStreamId);
        }

        [TestMethod]
        public async Task CreateObsoleteItem_WithValidData_ReturnsCreatedStatusCode()
        {
            var obsoleteItem = new ObsoleteItem
            {
                Id = "obsolete2",
                SpecificationId = "spec2",
                FundingStreamId = "FS1",
                DatasetRelationshipName = "Relationship2",
                ItemType = ObsoleteItemType.Calculation,
                DatasetDatatype = DatasetFieldType.Decimal,
                CodeReference = "Return 0",
                CalculationIds = new List<string> {}
            };

            var result = await _repository.CreateObsoleteItem(obsoleteItem);

            await _dbContext.SaveChangesAsync();

            Assert.AreEqual(HttpStatusCode.Created, result);

            var obsoleteItemInDb = _dbContext.ObsoleteItems.SingleOrDefault(o => o.ObsoleteItemId == "obsolete2");
            Assert.IsNotNull(obsoleteItemInDb);
            Assert.AreEqual("spec2", obsoleteItemInDb.SpecificationId);
            Assert.AreEqual(1, obsoleteItemInDb.FundingStreamId);
        }

        [TestMethod]
        public async Task DeleteObsoleteItem_WithValidId_PerformsDelete()
        {
            DetachAllDBEntries();

            await _repository.DeleteObsoleteItem("obsolete1");

            var obsoleteItem = _dbContext.ObsoleteItems.SingleOrDefault(o => o.ObsoleteItemId == "obsolete1");
            Assert.IsNull(obsoleteItem);
        }

        [TestMethod]
        public async Task GetCountOfNonApprovedTemplateCalculations_WithValidData_ReturnsCorrectCount()
        {
            var count = await _repository.GetCountOfNonApprovedTemplateCalculations("spec1");

            Assert.AreEqual(1, count); // Only "calc1" is not approved.
        }

        [TestMethod]
        public async Task IsHealthOk_ReturnsServiceHealth()
        {
            var result = await _repository.IsHealthOk();

            Assert.IsNotNull(result);
            Assert.AreEqual("CalculationsRepository", result.Name);
            Assert.IsTrue(result.Dependencies.Any(d => d.HealthOk));
        }


        [TestMethod]
        public async Task UpdateCalculations_WithValidData_PerformsUpdate()
        {
            // Arrange
            var calculationsToUpdate = new List<Calculation>{new Calculation { 
                Id = "calc1",
                Current = new CalculationVersion {
                    PublishStatus = Models.Versioning.PublishStatus.Updated
                }
            },
             new Calculation {
                 Id = "calc2", 
                 Current = new CalculationVersion {
                     PublishStatus = Models.Versioning.PublishStatus.Approved
                 }
             } 
            };

            DetachAllDBEntries();

            // Act
            await _repository.UpdateCalculations(calculationsToUpdate);

            await _dbContext.SaveChangesAsync();

            // Assert
            var updatedVersion1 = _dbContext.CalculationVersions.First(cv => cv.CalculationId == "calc1" && cv.IsLatest);
            var updatedVersion2 = _dbContext.CalculationVersions.First(cv => cv.CalculationId == "calc2" && cv.IsLatest);

            Assert.IsNotNull(updatedVersion1);
            Assert.AreEqual("Updated", updatedVersion1.PublishStatus);

            Assert.IsNotNull(updatedVersion2);
            Assert.AreEqual("Approved", updatedVersion2.PublishStatus);
        }

        [TestMethod]
        public async Task GetTemplateMapping_WithValidSpecificationId_ReturnsTemplateMapping()
        {
            // Arrange
            string specificationId = "spec1";
            string fundingStreamCode = "FS1";

            _dbContext.TemplateMappings.Add(new EntityModel.TemplateMapping
            {
                SpecificationId = specificationId,
                TemplateMappingId = $"templatemapping-{specificationId}-{fundingStreamCode}",
                FundingStreamId = 1
            });

            _dbContext.TemplateMappingItems.AddRange(new List<EntityModel.TemplateMappingItem> { 
                new EntityModel.TemplateMappingItem{
                    TemplateMappingId = $"templatemapping-{specificationId}-{fundingStreamCode}",
                    EntityType = 0,
                    CalculationId = "calc1",
                    Name = "Template Item 1"
            },
                new EntityModel.TemplateMappingItem{
                    TemplateMappingId = $"templatemapping-{specificationId}-{fundingStreamCode}",
                    EntityType = 0,
                    CalculationId = "calc2",
                    Name = "Template Item 2" }
            });

            _dbContext.SaveChanges();

            // Act
            var result = await _repository.GetTemplateMapping(specificationId, fundingStreamCode);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(fundingStreamCode, result.FundingStreamId);
            Assert.AreEqual(2, result.TemplateMappingItems.Count);
            Assert.IsTrue(result.TemplateMappingItems.Any(t => t.Name == "Template Item 1"));
            Assert.IsTrue(result.TemplateMappingItems.Any(t => t.Name == "Template Item 2"));
        }

        [TestMethod]
        public async Task GetStatusCounts_WithValidSpecification_ReturnsCounts()
        {
            DetachAllDBEntries();

            // Act
            var result = await _repository.GetStatusCounts("spec1");

            // Assert
            Assert.AreEqual(1, result.Approved);
            Assert.AreEqual(1, result.Draft);
            Assert.AreEqual(0, result.Updated);
        }

        [TestMethod]
        public async Task GetTemplateCalculationsBySpecificationId_WithValidSpecification_ReturnsTemplateCalculations()
        {
            // Act
            var result = await _repository.GetTemplateCalculationsBySpecificationId("spec1");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            Assert.AreEqual("calc1", result.First().Id);
        }

        [TestMethod]
        public async Task GetObsoleteItemById_WithValidId_ReturnsObsoleteItem()
        {
            // Arrange
            var obsoleteItemId = "obsolete1";

            // Act
            var result = await _repository.GetObsoleteItemById(obsoleteItemId);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(obsoleteItemId, result.Id);
            Assert.AreEqual("TestRelationship", result.DatasetRelationshipName);
        }

        [TestMethod]
        public async Task GetObsoleteItemsForCalculation_WithValidCalculationId_ReturnsObsoleteItems()
        {
            // Act
            var result = await _repository.GetObsoleteItemsForCalculation("calc1");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
            Assert.IsTrue(result.Any(o => o.Id == "obsolete1"));
        }

        [TestMethod]
        public async Task UpdateObsoleteItem_WithValidData_PerformsUpdate()
        {
            // Arrange
            var obsoleteItem = new ObsoleteItem
            {
                Id = "obsolete1",
                SpecificationId = "spec1",
                DatasetFieldName = "UpdatedField",
                DatasetRelationshipName = "TestRelationship",
                FundingStreamId = "FS1",
                CodeReference = "Return 0",
                ItemType = ObsoleteItemType.Calculation,
                IsReleasedData = false,
                CalculationIds = new List<string> { "calc1", "calc2" }
            };

            DetachAllDBEntries();

            // Act
            var result = await _repository.UpdateObsoleteItem(obsoleteItem);

            await _dbContext.SaveChangesAsync();

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, result);

            var updatedObsoleteItemCalcs = _dbContext.ObsoleteItemCalcs.Where(c => c.ObsoleteItemId == "obsolete1").ToList();
            Assert.AreEqual(2, updatedObsoleteItemCalcs.Count);
            Assert.IsTrue(updatedObsoleteItemCalcs.Any(c => c.CalculationId == "calc1"));
            Assert.IsTrue(updatedObsoleteItemCalcs.Any(c => c.CalculationId == "calc2"));
        }

        [TestMethod]
        public async Task DeleteTemplateMappingsBySpecificationId_WithSoftDelete_PerformsSoftDelete()
        {
            
            // Arrange
            _dbContext.TemplateMappings.Add(new EntityModel.TemplateMapping
            {
                TemplateMappingId = "templatemapping1",
                SpecificationId = "spec1",
                IsDeleted = false
            });

            await _dbContext.SaveChangesAsync();

            DetachAllDBEntries();

            // Act
            await _repository.DeleteTemplateMappingsBySpecificationId("spec1", DeletionType.SoftDelete);

            // Assert
            var templateMapping = await _dbContext.TemplateMappings.SingleOrDefaultAsync(t => t.TemplateMappingId == "templatemapping1");
            Assert.IsNotNull(templateMapping);
            Assert.IsTrue(templateMapping.IsDeleted);
        }


        [TestMethod]
        public async Task GetAllCalculations_ReturnsAllActiveCalculations()
        {
            // Act
            var result = await _repository.GetAllCalculations();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());
            Assert.IsTrue(result.Any(c => c.Id == "calc1"));
            Assert.IsTrue(result.Any(c => c.Id == "calc2"));
        }

        private void DetachAllDBEntries()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}