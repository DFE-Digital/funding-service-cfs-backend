using CalculateFunding.Common.EfCore.UnitOfWork;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using System;
using CalculateFunding.Common.EfCore.GenericRepository;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using Specification = CalculateFunding.Models.Specs.Specification;
using CalculateFunding.Models.Messages;

namespace CalculateFunding.Services.Specs.UnitTests
{
    [TestClass]
    public class SpecificationRepositoryTests
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected CfsDbContext _dbContext;
        protected SpecificationsRepository _specificationsRepository;

        public SpecificationRepositoryTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
            _specificationsRepository = new SpecificationsRepository(_uowMock.Object);
        }

        #region Get APIs

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetSpecificationBy_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _specificationsRepository.GetSpecificationById(null);
        }

        [TestMethod]
        public async Task GetSpecificationById_WithValidData_ReturnsSpecification()
        {
            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationById("1");

            Assert.IsNotNull(specResult);
            Assert.IsInstanceOfType(specResult, typeof(Specification));
        }

        [TestMethod]
        public async Task GetSpecificationById_WithMissingData_ReturnsNull()
        {
            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationById("3");

            Assert.IsNull(specResult);
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream_ThrowsArgumentException()
        {
            await _specificationsRepository.GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream(null, null);
        }

        [TestMethod]
        public async Task GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream_WithValidData_ReturnsSpecification()
        {
            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream("AS-123", "1619");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(2, specResult.Count());
            Assert.AreEqual(3, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }


        [TestMethod]
        public async Task GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream_WithValidData_ReturnsOneSpecification()
        {
            SetupTables();

            for (int i = 1; i <= 2; i++)
            {
                var specTableEntry = new EntityModel.Specification()
                {
                    ForceUpdateOnNextRefresh = true,
                    SpecificationId = i.ToString(),
                    IsDeleted = false,
                    SpecificationName = "Name-" + i,
                    FundingPeriodId = 1,
                    FundingStreamId = 1,
                    IsSelectedForFunding = false,
                };
                _dbContext.Specifications.Add(specTableEntry);


                var specVersionTableEntry = new EntityModel.SpecificationVersion()
                {
                    IsDeleted = false,
                    SpecificationVersionId = "v" + i,
                    SpecificationId = specTableEntry.SpecificationId,
                    SpecificationName = specTableEntry.SpecificationName,
                    Version = 1,
                    CoreProviderVersionUpdates = "Manual",
                    AuthorId = "id",
                    AuthorName = "UserName",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    PublishStatus = "Draft",
                    ProviderSource = "FDZ"
                };
                _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            }

            var specVersionEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v3",
                SpecificationId = "2",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Updated",
                ProviderSource = "FDZ",
                IsLatest = true
            };
            _dbContext.SpecificationVersions.Add(specVersionEntry);

            SetupDataForStreamAndPeriod();

            await _dbContext.SaveChangesAsync();

            var specResult = await _specificationsRepository.GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream("AS-123", "1619");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(specVersionEntry.Version, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetSpecificationsByFundingPeriodAndFundingStream_ThrowsArgumentException()
        {
            await _specificationsRepository.GetSpecificationsByFundingPeriodAndFundingStream(null, null);
        }


        [TestMethod]
        public async Task GetSpecificationsByFundingPeriodAndFundingStream_WithValidData_ReturnsSpecification()
        {
            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationsByFundingPeriodAndFundingStream("AS-123", "1619");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(2, specResult.Count());
            Assert.AreEqual(3, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetSpecificationsSelectedForFundingByPeriodAndFundingStream_ThrowsArgumentException()
        {
            await _specificationsRepository.GetSpecificationsSelectedForFundingByPeriodAndFundingStream(null, null);
        }


        [TestMethod]
        public async Task GetSpecificationsSelectedForFundingByPeriodAndFundingStream_WithValidData_ReturnsSpecification()
        {
            SetupTables();

            for (int i = 1; i <= 2; i++)
            {
                var specTableEntry = new EntityModel.Specification()
                {
                    ForceUpdateOnNextRefresh = true,
                    SpecificationId = i.ToString(),
                    IsDeleted = false,
                    SpecificationName = "Name-" + i,
                    FundingPeriodId = 1,
                    FundingStreamId = 1,
                    IsSelectedForFunding = true,
                };
                _dbContext.Specifications.Add(specTableEntry);


                var specVersionTableEntry = new EntityModel.SpecificationVersion()
                {
                    IsDeleted = false,
                    SpecificationVersionId = "v" + i,
                    SpecificationId = specTableEntry.SpecificationId,
                    SpecificationName = specTableEntry.SpecificationName,
                    Version = 1,
                    CoreProviderVersionUpdates = "Manual",
                    AuthorId = "id",
                    AuthorName = "UserName",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    PublishStatus = "Approved",
                    ProviderSource = "FDZ",
                    IsLatest = (i == 1)
                };
                _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            }


            var specVersionEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v3",
                SpecificationId = "2",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Updated",
                ProviderSource = "FDZ",
                IsLatest = true
            };
            _dbContext.SpecificationVersions.Add(specVersionEntry);

            SetupDataForStreamAndPeriod();

            await _dbContext.SaveChangesAsync();

            var specResult = await _specificationsRepository.GetSpecificationsSelectedForFundingByPeriodAndFundingStream("AS-123", "1619");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(2, specResult.Count());
            Assert.AreEqual(specVersionEntry.Version, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }


        [TestMethod]
        public async Task GetSpecificationsSelectedForFundingByPeriodAndFundingStream_WithValidData_ReturnsOneSpecification()
        {
            SetupDataForGetAPI();
            var specResult = await _specificationsRepository.GetSpecificationsSelectedForFundingByPeriodAndFundingStream("AS-123", "1619");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(1, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetSpecificationsSelectedForFundingByPeriod_ThrowsArgumentException()
        {
            await _specificationsRepository.GetSpecificationsSelectedForFundingByPeriod(null);
        }


        [TestMethod]
        public async Task GetSpecificationsSelectedForFundingByPeriod_WithValidData_ReturnsSpecification()
        {
            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationsSelectedForFundingByPeriod("AS-123");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(1, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }


        [TestMethod]
        public async Task GetSpecificationsSelectedForFundingByPeriod_WithDifferentFundingPeriods_ReturnsOneSpecification()
        {
            SetupTables();

            for (int i = 1; i <= 2; i++)
            {
                var specTableEntry = new EntityModel.Specification()
                {
                    ForceUpdateOnNextRefresh = true,
                    SpecificationId = i.ToString(),
                    IsDeleted = false,
                    SpecificationName = "Name-" + i,
                    FundingPeriodId = i,
                    FundingStreamId = 1,
                    IsSelectedForFunding = true,
                };
                _dbContext.Specifications.Add(specTableEntry);


                var specVersionTableEntry = new EntityModel.SpecificationVersion()
                {
                    IsDeleted = false,
                    SpecificationVersionId = "v" + i,
                    SpecificationId = specTableEntry.SpecificationId,
                    SpecificationName = specTableEntry.SpecificationName,
                    Version = 1,
                    CoreProviderVersionUpdates = "Manual",
                    AuthorId = "id",
                    AuthorName = "UserName",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    PublishStatus = "Approved",
                    ProviderSource = "FDZ"
                };
                _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            }

            var specVersionEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v3",
                SpecificationId = "2",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Updated",
                ProviderSource = "FDZ",
                IsLatest = true
            };
            _dbContext.SpecificationVersions.Add(specVersionEntry);

            for (int i = 1; i <= 2; i++)
            {
                _dbContext.FundingPeriods.Add(new EntityModel.FundingPeriod()
                {
                    FundingPeriodId = i,
                    EndDate = DateTime.UtcNow,
                    StartDate = DateTime.UtcNow,
                    FundingPeriodCode = i.ToString(),
                    FundingPeriodName = "Name" + i,
                });
            }

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

            await _dbContext.SaveChangesAsync();

            var specResult = await _specificationsRepository.GetSpecificationsSelectedForFundingByPeriod("2");

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(specVersionEntry.Version, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }


        [TestMethod]
        public async Task GetSpecifications_WithValidData_ReturnsSpecification()
        {
            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecifications();

            Assert.IsNotNull(specResult);
            Assert.AreEqual(2, specResult.Count());
            Assert.AreEqual(3, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }


        [TestMethod]
        public async Task GetSpecifications_WithDeletedSpec_ReturnsOneSpecification()
        {
            SetupTables();

            for (int i = 1; i <= 2; i++)
            {
                var specTableEntry = new EntityModel.Specification()
                {
                    ForceUpdateOnNextRefresh = true,
                    SpecificationId = i.ToString(),
                    IsDeleted = (i == 1),
                    SpecificationName = "Name-" + i,
                    FundingPeriodId = 1,
                    FundingStreamId = 1,
                    IsSelectedForFunding = true,
                };
                _dbContext.Specifications.Add(specTableEntry);


                var specVersionTableEntry = new EntityModel.SpecificationVersion()
                {
                    IsDeleted = false,
                    SpecificationVersionId = "v" + i,
                    SpecificationId = specTableEntry.SpecificationId,
                    SpecificationName = specTableEntry.SpecificationName,
                    Version = 1,
                    CoreProviderVersionUpdates = "Manual",
                    AuthorId = "id",
                    AuthorName = "UserName",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    PublishStatus = "Approved",
                    ProviderSource = "FDZ"
                };
                _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            }


            var specVersionEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v3",
                SpecificationId = "2",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Updated",
                ProviderSource = "FDZ",
                IsLatest = true
            };
            _dbContext.SpecificationVersions.Add(specVersionEntry);

            SetupDataForStreamAndPeriod();

            await _dbContext.SaveChangesAsync();

            var specResult = await _specificationsRepository.GetSpecifications();

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(specVersionEntry.Version, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));

        }
        #endregion


        #region Get by Query API


        [TestMethod]
        public async Task GetSpecificationsBySQLQuery_WithValidData_ReturnsSelectedSpecification()
        {

            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationsBySQLQuery(_ => _.IsSelectedForFunding);

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(1, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));
        }

        [TestMethod]
        public async Task GetSpecificationsBySQLQuery_WithValidData_ReturnsMatchingSpecificationWithName()
        {

            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationsBySQLQuery(_ => _.SpecificationName.Equals("Name-2"));

            Assert.IsNotNull(specResult);
            Assert.AreEqual(1, specResult.Count());
            Assert.AreEqual(3, specResult.LastOrDefault().Current.Version);
            Assert.IsInstanceOfType(specResult, typeof(IEnumerable<Specification>));
        }

        [TestMethod]
        public async Task GetSpecificationsBySQLQuery_WithInvalidSpecId_ReturnsNull()
        {

            SetupDataForGetAPI();

            var specResult = await _specificationsRepository.GetSpecificationsBySQLQuery(_ => _.SpecificationId.Equals("id"));

            Assert.IsNull(specResult);
        }

        #endregion

        #region Create API
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]

        public async Task CreateSpecificationThrowArgumentException()
        {

            await _specificationsRepository.CreateSpecification(null);

        }

        [TestMethod]
        public async Task CreateSpecification_WithValidData()
        {
            SetupTables();
            SetupDataForStreamAndPeriod();

            await _dbContext.SaveChangesAsync();

            var specification = new Specification()
            {
                Id = "1",
                ForceUpdateOnNextRefresh = true,
                IsSelectedForFunding = true,
                Name = "1619",
                Current = new Models.Specs.SpecificationVersion()
                {
                    FundingPeriod = new Common.Models.Reference()
                    {
                        Id = "AS-123",
                        Name = "AS-123"
                    },
                    FundingStreams = new List<Common.Models.Reference>()
                     {
                         new Common.Models.Reference()
                         {
                             Name = "1619",
                             Id = "1619",
                         }
                     }
                }
            };

            var result = await _specificationsRepository.CreateSpecification(specification);

            Assert.IsNotNull(result);

        }

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))]
        public async Task CreateSpecification_WithoutPeriod_ThrowsException()
        {
            var specification = new Specification()
            {
                Id = "1",
                ForceUpdateOnNextRefresh = true,
                IsSelectedForFunding = true,
                Name = "1619",
                Current = new Models.Specs.SpecificationVersion()
                {
                    FundingPeriod = new Common.Models.Reference()
                    {
                        Id = "AS-123",
                        Name = "AS-123"
                    },
                    FundingStreams = new List<Common.Models.Reference>()
                     {
                         new Common.Models.Reference()
                         {
                             Name = "1619",
                             Id = "1619",
                         }
                     }
                }
            };

            var result = await _specificationsRepository.CreateSpecification(specification);

            Assert.IsNotNull(result);

        }
        #endregion

        #region Update API
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task UpdateSpecification_ThrowArgumentException()
        {
            await _specificationsRepository.UpdateSpecification(null);
        }

        [TestMethod]
        public async Task UpdateSpecification_WithValidData_ReturnsCode()
        {
            SetupDataForGetAPI();

            var specification = new Specification()
            {
                Id = "1",
                ForceUpdateOnNextRefresh = true,
                IsSelectedForFunding = true,
                Name = "1619",
                Current = new Models.Specs.SpecificationVersion()
                {
                    FundingPeriod = new Common.Models.Reference()
                    {
                        Id = "AS-123",
                        Name = "AS-123"
                    },
                    FundingStreams = new List<Common.Models.Reference>()
                     {
                         new Common.Models.Reference()
                         {
                             Name = "1619",
                             Id = "1619",
                         }
                     }
                }
            };

            DetachAllDBEntries();

            var httpCode = await _specificationsRepository.UpdateSpecification(specification);

            Assert.IsTrue(httpCode == System.Net.HttpStatusCode.OK);

        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task UpdateSpecification_WithValidData_ReturnsInvalidOpperationException()
        {
            SetupDataForGetAPI();

            var specification = new Specification()
            {
                Id = "11",
                ForceUpdateOnNextRefresh = true,
                IsSelectedForFunding = true,
                Name = "1619Recon",
                Current = new Models.Specs.SpecificationVersion()
                {
                    FundingPeriod = new Common.Models.Reference()
                    {
                        Id = "AS-123",
                        Name = "AS-123"
                    },
                    FundingStreams = new List<Common.Models.Reference>()
                     {
                         new Common.Models.Reference()
                         {
                             Name = "1619",
                             Id = "1619",
                         }
                     }
                }
            };

            DetachAllDBEntries();

            await _specificationsRepository.UpdateSpecification(specification);
        }

        #endregion

        #region Delete API
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task DeleteSpecification_ThrowsArgumentException()
        {
            string deletionTypeProperty = "Soft Delete";
            DeletionType deletionType = deletionTypeProperty.ToDeletionType();
            await _specificationsRepository.DeleteSpecifications(null, deletionType);
        }

        [TestMethod]
        public async Task DeleteSpecification_WithValidData_SoftDelete()
        {
            SetupDataForGetAPI();

            DetachAllDBEntries();

            await _specificationsRepository.DeleteSpecifications("1", DeletionType.SoftDelete);

            Assert.IsTrue(_dbContext.Specifications.FirstOrDefault().IsDeleted);

            Assert.IsNull(_dbContext.Specifications.Where(_ => _.IsDeleted).FirstOrDefault());
        }

        #endregion

        #region Other Get APIs

        [TestMethod]
        [ExpectedException(typeof(NullReferenceException))]
        public async Task GetDistinctProviderVersionIdsFromSpecifications_ThrowsArgumentException()
        {
            var result = await _specificationsRepository.GetDistinctProviderVersionIdsFromSpecifications(null);
        }


        [TestMethod]
        public async Task GetDistinctProviderVersionIdsFromSpecifications_WithValidData_ReturnsListOfProviderVersionIds()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Specification>()).Returns(new GenericRepository<EntityModel.Specification>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.SpecificationVersion>()).Returns(new GenericRepository<EntityModel.SpecificationVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DefinitionSpecificationRelationship>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.VariationPointer>()).Returns(new GenericRepository<EntityModel.VariationPointer>(_dbContext));

            var specTableEntry = new EntityModel.Specification()
            {
                ForceUpdateOnNextRefresh = true,
                SpecificationId = "1",
                IsDeleted = false,
                SpecificationName = "Name",
                FundingPeriodId = 1,
                FundingStreamId = 1,
                IsSelectedForFunding = false,
            };
            _dbContext.Specifications.Add(specTableEntry);

            var specVersionTableEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v1",
                SpecificationId = "1",
                SpecificationName = "Name",
                Version = 1,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Draft",
                ProviderSource = "FDZ",
                ProviderVersionId = "pid",
                IsLatest = true
            };
            _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            var fundingPeriodTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodId = 1,
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                FundingPeriodCode = "AS-123",
                FundingPeriodName = "Name",
            };
            _dbContext.FundingPeriods.Add(fundingPeriodTableEntry);

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _specificationsRepository.GetDistinctProviderVersionIdsFromSpecifications(new string[] { "1" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count() == 1);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<string>));

        }


        [TestMethod]
        public async Task GetDistinctProviderVersionIdsFromSpecifications_WithMissingData_ReturnsListOfProviderVersionIds()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Specification>()).Returns(new GenericRepository<EntityModel.Specification>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.SpecificationVersion>()).Returns(new GenericRepository<EntityModel.SpecificationVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));

            var specTableEntry = new EntityModel.Specification()
            {
                ForceUpdateOnNextRefresh = true,
                SpecificationId = "1",
                IsDeleted = false,
                SpecificationName = "Name",
                FundingPeriodId = 1,
                FundingStreamId = 1,
                IsSelectedForFunding = false,
            };
            _dbContext.Specifications.Add(specTableEntry);

            var specVersionTableEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v1",
                SpecificationId = "1",
                SpecificationName = "Name",
                Version = 1,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Draft",
                ProviderSource = "FDZ",
                ProviderVersionId = "pid",
            };
            _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            var fundingPeriodTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodId = 1,
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                FundingPeriodCode = "AS-123",
                FundingPeriodName = "Name",
            };
            _dbContext.FundingPeriods.Add(fundingPeriodTableEntry);

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _specificationsRepository.GetDistinctProviderVersionIdsFromSpecifications(new string[] { "2" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count() == 0);

        }


        [TestMethod]
        public async Task GetDistinctFundingStreamsForSpecifications_WithValidData_ReturnsListOfProviderVersionIds()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Specification>()).Returns(new GenericRepository<EntityModel.Specification>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.SpecificationVersion>()).Returns(new GenericRepository<EntityModel.SpecificationVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DefinitionSpecificationRelationship>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.VariationPointer>()).Returns(new GenericRepository<EntityModel.VariationPointer>(_dbContext));

            var specTableEntry = new EntityModel.Specification()
            {
                ForceUpdateOnNextRefresh = true,
                SpecificationId = "1",
                IsDeleted = false,
                SpecificationName = "Name",
                FundingPeriodId = 1,
                FundingStreamId = 1,
                IsSelectedForFunding = false,
            };
            _dbContext.Specifications.Add(specTableEntry);

            var specVersionTableEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v1",
                SpecificationId = "1",
                SpecificationName = "Name",
                Version = 1,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Draft",
                ProviderSource = "FDZ",
                ProviderVersionId = "pid",
            };
            _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            var fundingPeriodTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodId = 1,
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                FundingPeriodCode = "AS-123",
                FundingPeriodName = "Name",
            };
            _dbContext.FundingPeriods.Add(fundingPeriodTableEntry);

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _specificationsRepository.GetDistinctFundingStreamsForSpecifications();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count() == 1);
            Assert.IsInstanceOfType(result, typeof(IEnumerable<string>));

        }

        #endregion

        #region Private Methods

        private void DetachAllDBEntries()
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
        }

        private void SetupTables()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Specification>()).Returns(new GenericRepository<EntityModel.Specification>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.SpecificationVersion>()).Returns(new GenericRepository<EntityModel.SpecificationVersion>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DatasetSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DatasetSpecificationRelationship>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()).Returns(new GenericRepository<EntityModel.DefinitionSpecificationRelationship>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<EntityModel.VariationPointer>()).Returns(new GenericRepository<EntityModel.VariationPointer>(_dbContext));

        }

        private async void SetupDataForGetAPI()
        {
            SetupTables();

            for (int i = 1; i <= 2; i++)
            {
                var specTableEntry = new EntityModel.Specification()
                {
                    ForceUpdateOnNextRefresh = true,
                    SpecificationId = i.ToString(),
                    IsDeleted = false,
                    SpecificationName = "Name-" + i,
                    FundingPeriodId = 1,
                    FundingStreamId = 1,
                    IsSelectedForFunding = (i == 1),
                };
                _dbContext.Specifications.Add(specTableEntry);

                var specVersionTableEntry = new EntityModel.SpecificationVersion()
                {
                    IsDeleted = false,
                    SpecificationVersionId = "v" + i,
                    SpecificationId = specTableEntry.SpecificationId,
                    SpecificationName = specTableEntry.SpecificationName,
                    Version = 1,
                    CoreProviderVersionUpdates = "Manual",
                    AuthorId = "id",
                    AuthorName = "UserName",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Date = DateTime.UtcNow,
                    PublishStatus = "Approved",
                    ProviderSource = "FDZ",
                    IsLatest = (i == 1)
                };
                _dbContext.SpecificationVersions.Add(specVersionTableEntry);

            }

            var specVersionEntry = new EntityModel.SpecificationVersion()
            {
                IsDeleted = false,
                SpecificationVersionId = "v3",
                SpecificationId = "2",
                SpecificationName = "Name-2",
                Version = 3,
                CoreProviderVersionUpdates = "Manual",
                AuthorId = "id",
                AuthorName = "UserName",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                PublishStatus = "Updated",
                ProviderSource = "FDZ",
                IsLatest = true
            };
            _dbContext.SpecificationVersions.Add(specVersionEntry);

            var fundingPeriodTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodId = 1,
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                FundingPeriodCode = "AS-123",
                FundingPeriodName = "Name",
            };
            _dbContext.FundingPeriods.Add(fundingPeriodTableEntry);

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

            await _dbContext.SaveChangesAsync();
        }

        private async void SetupDataForStreamAndPeriod()
        {
            var fundingPeriodTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodId = 1,
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                FundingPeriodCode = "AS-123",
                FundingPeriodName = "Name",
            };
            _dbContext.FundingPeriods.Add(fundingPeriodTableEntry);

            var fundingStreamTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamId = 1,
                FundingStreamCode = "1619",
                FundingStreamName = "Name",
            };
            _dbContext.FundingStreams.Add(fundingStreamTableEntry);

        }

        #endregion
    }
}
