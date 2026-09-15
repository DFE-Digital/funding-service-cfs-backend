using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Services.Datasets.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Datasets.UnitTests.Services
{
    [TestClass]
    public class DatasetsAggregationsRepositoryTests
    {
        private Mock<IUnitOfWork> _uowMock;
        private Mock<DbContext> _dbContextMock;
        private Mock<DatabaseFacade> _databaseMock;
        private DatasetsAggregationsRepository _repository;

        // Null UnitOfWork check
        [TestInitialize]
        public void Setup()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContextMock = new Mock<DbContext>(new DbContextOptions<DbContext>());
            _databaseMock = new Mock<DatabaseFacade>(_dbContextMock.Object);

            _dbContextMock.Setup(x => x.Database).Returns(_databaseMock.Object);
            _uowMock.Setup(x => x.context).Returns(_dbContextMock.Object);

            _repository = new DatasetsAggregationsRepository(_uowMock.Object);
        }

        // Constructor null check
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullUnitOfWork_ThrowsException()
        {
            new DatasetsAggregationsRepository(null);
        }

        // Health check - healthy
        [TestMethod]
        public async Task IsHealthOk_WhenDatabaseCanConnect_ReturnsHealthy()
        {
            _databaseMock.Setup(db => db.CanConnect()).Returns(true);

            ServiceHealth result = await _repository.IsHealthOk();

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(DatasetsAggregationsRepository), result.Name);
        }

        // Health check - Unhealthy
        [TestMethod]
        public async Task IsHealthOk_WhenDatabaseCannotConnect_ReturnsUnhealthy()
        {
            _databaseMock.Setup(db => db.CanConnect()).Returns(false);

            ServiceHealth result = await _repository.IsHealthOk();

            Assert.IsNotNull(result);
            Assert.AreEqual(nameof(DatasetsAggregationsRepository), result.Name);
        }

        // CreateDatasetAggregations null input
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CreateDatasetAggregations_WhenNull_ThrowsException()
        {
            await _repository.CreateDatasetAggregations(null);
        }

        // CreateDatasetAggregations Upsert and commits
        [TestMethod]
        public async Task CreateDatasetAggregations_UpsertAndCommits()
        {
            var repoMock = new Mock<IDatasetsAggregationsRepository>();

            repoMock
                .Setup(r => r.CreateDatasetAggregations(It.IsAny<DatasetAggregations>()))
                .Returns(Task.CompletedTask);

            var input = new Models.Datasets.DatasetAggregations
            {
                SpecificationId = "03196821-f784-4f7d-8a37-736f5fd33767",
                DatasetRelationshipId = "74bf86e5-fefa-4ee4-a78e-cd3d5aff3abb",
            };

            await repoMock.Object.CreateDatasetAggregations(input);

            repoMock.Verify(r => r.CreateDatasetAggregations(input), Times.Once);
        }

        // GetDatasetAggregationsForSpecificationId null input
        [TestMethod]
        public async Task GetDatasetAggregationsForSpecificationId_WhenValidId_ReturnsExpectedData()
        {
            // Arrange
            string specificationId = "spec-123";

            var expected = new List<DatasetAggregations>
            {
                new DatasetAggregations
                {
                    SpecificationId = specificationId,
                    DatasetRelationshipId = "rel-1",
                    Fields = new List<AggregatedField>
                    {
                        new AggregatedField
                        {
                            FieldDefinitionName = "TotalPupils",
                            Value = 1200m,
                            FieldType = AggregatedTypes.Sum
                        }
                    }
                }
             };

            var repo = new Mock<IDatasetsAggregationsRepository>();
            repo.Setup(r => r.GetDatasetAggregationsForSpecificationId(specificationId))
                .ReturnsAsync(expected);

            // Act
            var result = await repo.Object.GetDatasetAggregationsForSpecificationId(specificationId);

            // Assert
            Assert.IsNotNull(result);

            var list = result.ToList();
            Assert.AreEqual(1, list.Count);

            var aggregation = list[0];

            Assert.AreEqual("spec-123", aggregation.SpecificationId);
            Assert.AreEqual("rel-1", aggregation.DatasetRelationshipId);
            Assert.AreEqual("spec-123_rel-1", aggregation.Id);  // derived Id

            // Field validation
            var fields = aggregation.Fields.ToList();
            Assert.AreEqual(1, fields.Count);

            var field = fields[0];
            Assert.AreEqual("TotalPupils", field.FieldDefinitionName);
            Assert.AreEqual(1200m, field.Value);
            Assert.AreEqual(AggregatedTypes.Sum, field.FieldType);

            // Verify the derived property
            Assert.AreEqual("TotalPupils_Sum", field.FieldReference);
        }

        // GetDatasetAggregations null input
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetDatasetAggregations_WhenNull_ThrowsException()
        {
            await _repository.GetDatasetAggregationsForSpecificationId(null);
        }             
    }
}
