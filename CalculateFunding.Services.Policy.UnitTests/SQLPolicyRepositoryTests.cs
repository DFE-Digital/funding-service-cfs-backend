using CalculateFunding.Common.EfCore.UnitOfWork;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using System;
using CalculateFunding.Common.EfCore.GenericRepository;
using Newtonsoft.Json;
using PolicyModel = CalculateFunding.Models.Policy;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Models.Policy.FundingPolicy;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Linq;
using System.Collections.Generic;

namespace CalculateFunding.Services.Policy.UnitTests
{
    [TestClass]
    public class SQLPolicyRepositoryTests
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected SQLPolicyRepository _policyRepository;
        protected CfsDbContext _dbContext;
        public SQLPolicyRepositoryTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _policyRepository = new SQLPolicyRepository(_uowMock.Object);
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options);
        }

        #region FundingDate Tests
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFundingDate_WithNullOrWhitespaceDateId_ThrowsArgumentException()
        {
            await _policyRepository.GetFundingDate(null);
        }


        [TestMethod]
        public async Task GetFundingDate_WithValidDateId_ReturnsFundingDate()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            
            var tableEntry = new EntityModel.Policy 
            { 
                Id = 1, 
                DocumentType = nameof(FundingDate), 
                PolicyId = "1", 
                IsDeleted = false, 
                Content = JsonConvert.SerializeObject(new FundingDate { /* initialize the date object */ }) 
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();
            var result = await _policyRepository.GetFundingDate("1");
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(FundingDate));

        }


        [TestMethod]
        public async Task GetFundingDate_WithValidContent_ReturnsFundingDate()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            
            var fundingDate = new FundingDate
            {
                FundingLineId = "1",
                FundingPeriodId = "1",
                FundingStreamId = "1",
                Id = "1"
            };
            
            var tableEntry = new EntityModel.Policy
            {
                Id = 1,
                DocumentType = nameof(FundingDate),
                PolicyId = "1",
                IsDeleted = false,
                Content = JsonConvert.SerializeObject(fundingDate )
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingDate("1");
           
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(FundingDate));
            Assert.AreEqual(fundingDate.Id, result.Id);

        }


        [TestMethod]
        public async Task SaveFundingDate_WithValidContent_ReturnsFundingDate()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));
            
            var fundingDate = new FundingDate
            {
                FundingLineId = "1",
                FundingPeriodId = "1",
                FundingStreamId = "1",
                Id = "1"
            };

            var fsTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);


            var fpTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodCode = "1",
                FundingPeriodName = "1",
                FundingPeriodId = 1
            };

            _dbContext.FundingPeriods.Add(fpTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.SaveFundingDate(fundingDate);

            Assert.AreEqual(result, HttpStatusCode.Created);

        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveFundingDate_WithNullInput_ThrowsArgumentException()
        {
            await _policyRepository.SaveFundingDate(null);
        }

        #endregion

        #region FundingStream Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFundingStreamById_WithNullOrWhitespaceDateId_ThrowsArgumentException()
        {
            await _policyRepository.GetFundingStreamById(null);
        }

        [TestMethod]
        public async Task GetFundingStreamById_WithValidId()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_dbContext));
            
            var fsTableEntry = new FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);

            await _dbContext.SaveChangesAsync();
            
            var result = await _policyRepository.GetFundingStreamById("1");
            
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(PolicyModel.FundingStream));
            Assert.AreEqual(fsTableEntry.FundingStreamCode, result.Id);
        }

        [TestMethod]
        public async Task GetFundingStreamById_WithInValidId_ReturnsNull()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_dbContext));

            var fsTableEntry = new FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingStreamById("2");
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetFundingStreams_WithValidData()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_dbContext));
            
            var fsTableEntry = new FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);

            await _dbContext.SaveChangesAsync();
            
            var result = await _policyRepository.GetFundingStreams();
            
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result.FirstOrDefault(), typeof(PolicyModel.FundingStream));
        }

        [TestMethod]
        public async Task SaveFundingStream_Successfully()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<FundingStream>(_dbContext));
            
            var fundingStream = new PolicyModel.FundingStream()
            {
                Id = "1",
                Name = "PNA",
                ShortName = "PNA"
            };

            var result = await _policyRepository.SaveFundingStream(fundingStream);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(result, HttpStatusCode.Created);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveFundingStream_WithNullInput_ThrowsArgumentException()
        {
            await _policyRepository.SaveFundingStream(null);
        }
        #endregion

        #region FundingPeriod Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFundingPeriodById_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _policyRepository.GetFundingPeriodById(null);
        }

        [TestMethod]
        public async Task GetFundingPeriodById_WithValidId()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_dbContext));
            
            var fpTableEntry = new FundingPeriod()
            {
                FundingPeriodCode = "1",
                FundingPeriodId = 1,
                FundingPeriodName = "1",
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                Type = "AC"
            };

            _dbContext.FundingPeriods.Add(fpTableEntry);

            await _dbContext.SaveChangesAsync();
           
            var result = await _policyRepository.GetFundingPeriodById("1");
            
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(PolicyModel.FundingPeriod));
            Assert.AreEqual(fpTableEntry.FundingPeriodCode, result.Id);
        }


        [TestMethod]
        public async Task GetFundingPeriodById_WithInValidId_ReturnsNull()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_dbContext));

            var fpTableEntry = new FundingPeriod()
            {
                FundingPeriodCode = "1",
                FundingPeriodId = 1,
                FundingPeriodName = "1",
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                Type = "AC"
            };

            _dbContext.FundingPeriods.Add(fpTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingPeriodById("2");
            Assert.IsNull(result);

        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GetFundingPeriodById_ThrowsArgumentException()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_dbContext));
            
            var fpTableEntry = new FundingPeriod()
            {
                FundingPeriodCode = "1",
                FundingPeriodId = 1,
                FundingPeriodName = "1"
            };

            _dbContext.FundingPeriods.Add(fpTableEntry);

            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingPeriodById("1");
        }

        [TestMethod]
        public async Task GetFundingPeriods_WithValidId()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_dbContext));
            
            var fpTableEntry = new FundingPeriod()
            {
                FundingPeriodCode = "1",
                FundingPeriodId = 1,
                FundingPeriodName = "1",
                EndDate = DateTime.UtcNow,
                StartDate = DateTime.UtcNow,
                Type = "AC"
            };

            _dbContext.FundingPeriods.Add(fpTableEntry);

            await _dbContext.SaveChangesAsync();
            
            var result = await _policyRepository.GetFundingPeriods();
            
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result.FirstOrDefault(), typeof(PolicyModel.FundingPeriod));
            Assert.AreEqual(fpTableEntry.FundingPeriodCode, result.FirstOrDefault().Id);
        }

        [TestMethod]
        public async Task SaveFundingPeriods_WithValidData()
        {
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<FundingPeriod>(_dbContext));
            
            var fps = new List<PolicyModel.FundingPeriod>()
            {
               new PolicyModel.FundingPeriod()
               {
                    Id = "1",
                    Period="1920",
                    Name = "1",
                    Type = PolicyModel.FundingPeriodType.AS,
                    EndDate = DateTime.UtcNow,
                    StartDate = DateTime.UtcNow
               }
            };

            await _policyRepository.SaveFundingPeriods(fps);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SaveFundingPeriod_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _policyRepository.SaveFundingPeriods(null);
        }
        #endregion

        #region FundingConfigurations Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFundingConfigurationById_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _policyRepository.GetFundingConfiguration(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetFundingConfigurationByFundingStreamId_WithNullOrWhitespace_ThrowsArgumentException()
        {
            await _policyRepository.GetFundingConfigurationsByFundingStreamId(null);
        }


        [TestMethod]
        public async Task GetFundingConfiguration_WithValidData_ReturnsFundingConfiguration()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            
            var tableEntry = new EntityModel.Policy
            {
                Id = 1,
                DocumentType = nameof(FundingConfiguration),
                PolicyId = "1",
                IsDeleted = false,
                Content = JsonConvert.SerializeObject(new FundingConfiguration { /* initialize the date object */ })
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();
            
            var result = await _policyRepository.GetFundingConfiguration("1");
           
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(FundingConfiguration));
        }

        [TestMethod]
        public async Task GetFundingConfiguration_WithMissingId_ReturnsNull()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));

            var tableEntry = new EntityModel.Policy
            {
                Id = 1,
                DocumentType = nameof(FundingConfiguration),
                PolicyId = "1",
                IsDeleted = false,
                Content = JsonConvert.SerializeObject(new FundingConfiguration { /* initialize the date object */ })
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingConfiguration("2");
            Assert.IsNull(result);

        }

        [TestMethod]
        public async Task GetFundingConfiguration_WithFundingStreamId_ReturnsFundingConfiguration()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));

            var fundingDate = new FundingDate
            {
                FundingLineId = "1",
                FundingPeriodId = "1",
                FundingStreamId = "1",
                Id = "1"
            };

            var fsTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);

            await _dbContext.SaveChangesAsync();

            var tableEntry = new EntityModel.Policy
            {
                Id = 1,
                DocumentType = nameof(FundingConfiguration),
                PolicyId = "1",
                IsDeleted = false,
                FundingStreamId = 1,
                Content = JsonConvert.SerializeObject(
                    new FundingConfiguration
                    {
                        FundingStreamId = "1"
                    })
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingConfigurationsByFundingStreamId("1");

            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result.FirstOrDefault(), typeof(FundingConfiguration));
        }

        [TestMethod]
        public async Task GetFundingConfiguration_WithFundingStreamId_ReturnsEmptyItems()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));

            var fundingDate = new FundingDate
            {
                FundingLineId = "1",
                FundingPeriodId = "1",
                FundingStreamId = "1",
                Id = "1"
            };

            var fsTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);

            await _dbContext.SaveChangesAsync();

            var tableEntry = new EntityModel.Policy
            {
                Id = 1,
                DocumentType = nameof(FundingConfiguration),
                PolicyId = "1",
                IsDeleted = false,
                Content = JsonConvert.SerializeObject(
                    new FundingConfiguration
                    {
                        FundingStreamId = "1"
                    })
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();

            var result = await _policyRepository.GetFundingConfigurationsByFundingStreamId("1");
            Assert.IsTrue(result.Count() == 0);

        }

        [TestMethod]
        public async Task GetFundingConfiguration_WithRandomId_ReturnsNull()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));

            var result = await _policyRepository.GetFundingConfiguration("1");

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetFundingConfigurations_WithValidData_ReturnsFundingConfigurations()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            
            var tableEntry = new EntityModel.Policy
            {
                Id = 1,
                DocumentType = nameof(FundingConfiguration),
                PolicyId = "1",
                IsDeleted = false,
                Content = JsonConvert.SerializeObject(new FundingConfiguration { /* initialize the date object */ })
            };

            _dbContext.Policies.Add(tableEntry);
            await _dbContext.SaveChangesAsync();
            
            var result = await _policyRepository.GetFundingConfigurations();
            
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result.FirstOrDefault(), typeof(FundingConfiguration));
        }

        [TestMethod]
        public async Task SaveFundingConfigurations_WithValidData_ReturnsStatuscodeCreated()
        {
            _uowMock.Setup(x => x.GenericRepository<EntityModel.Policy>()).Returns(new GenericRepository<EntityModel.Policy>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingStream>()).Returns(new GenericRepository<EntityModel.FundingStream>(_dbContext));
            _uowMock.Setup(x => x.GenericRepository<FundingPeriod>()).Returns(new GenericRepository<EntityModel.FundingPeriod>(_dbContext));
            
            var fundingDate = new FundingDate
            {
                FundingLineId = "1",
                FundingPeriodId = "1",
                FundingStreamId = "1",
                Id = "1"
            };

            var fsTableEntry = new EntityModel.FundingStream()
            {
                FundingStreamCode = "1",
                FundingStreamName = "1",
                FundingStreamId = 1
            };

            _dbContext.FundingStreams.Add(fsTableEntry);


            var fpTableEntry = new EntityModel.FundingPeriod()
            {
                FundingPeriodCode = "1",
                FundingPeriodName = "1",
                FundingPeriodId = 1
            };

            _dbContext.FundingPeriods.Add(fpTableEntry);

            await _dbContext.SaveChangesAsync();

            var fc = new FundingConfiguration
            {
                Id = "1",
                FundingStreamId = "1",
                FundingPeriodId = "1"
            };

            var result = await _policyRepository.SaveFundingConfiguration(fc);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(result, HttpStatusCode.Created);
        }

        #endregion
    }

}
