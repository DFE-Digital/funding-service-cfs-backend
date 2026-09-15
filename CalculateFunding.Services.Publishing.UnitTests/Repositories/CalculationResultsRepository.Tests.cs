using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Publishing.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.UnitTests.Repositories
{
    [TestClass]
    public class CalculationResultsRepositoryTest
    {
        protected Mock<IUnitOfWork> _uowMock;
        protected CalculationResultsRepository _repository;
        protected CfsDbContext _dbContext;

        public CalculationResultsRepositoryTest()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _repository = new CalculationResultsRepository(_uowMock.Object);
            _dbContext = new CfsDbContext(new DbContextOptionsBuilder<CfsDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        }

        #region GetCalculationResultsBySpecificationAndProvider

        [TestMethod]
        public async Task GetCalculationResultsBySpecificationAndProvider_ValidScenario()
        {
            SetupProviderResultData();
            SetupCalcResultData();

            var result = await _repository.GetCalculationResultsBySpecificationAndProvider("SPEC001", "PROV001");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());            
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task GetCalculationResultsBySpecificationAndProvider_FailedScenario_InvalidData_NullVersionIdThrowsArgumentNullException()
        {
            await _repository.GetCalculationResultsBySpecificationAndProvider("" , null);
        }

        [TestMethod]
        public async Task GetCalculationResultsBySpecificationAndProvider_EdgeCase_DataNotFound()
        {
            SetupTableProviderResult();
            SetupTableCalcResult();

            var result = await _repository.GetCalculationResultsBySpecificationAndProvider("specId", "provId");

            result.Should().BeNullOrEmpty();
        }

        #endregion

        #region Mock-Database setup

        private async void SetupTableProviderResult()
        {
            _uowMock.Setup(x => x.GenericRepository<ProviderResult>()).Returns(new GenericRepository<ProviderResult>(_dbContext));
        }

        private async void SetupTableCalcResult()
        {
            _uowMock.Setup(x => x.GenericRepository<CalcResult>()).Returns(new GenericRepository<CalcResult>(_dbContext));
        }

        private async void SetupProviderResultData()
        {
            SetupTableProviderResult();

            _uowMock.Setup(x => x.GenericRepository<ProviderResult>())
                .Returns(new GenericRepository<ProviderResult>(_dbContext));

            _uowMock.Setup(x => x.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());

            var data = new List<ProviderResult>
            {
                new ProviderResult
                {
                    ProviderResultId = "PR001",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    SpecificationId = "SPEC001",
                    IsIndicativeProvider = false,
                    UpdatedAt = DateTime.UtcNow.AddDays(-1),
                    IsDeleted = false,
                    ProviderId = "PROV001",
                    ProviderVersionId = "PV001"
                },                
            };

            _dbContext.ProviderResults.AddRange(data);
            await _dbContext.SaveChangesAsync();

            foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        private async void SetupCalcResultData()
        {
            SetupTableCalcResult();

            _uowMock.Setup(x => x.GenericRepository<CalcResult>())
                .Returns(new GenericRepository<CalcResult>(_dbContext));

            _uowMock.Setup(x => x.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());

            var data = new List<CalcResult>
            {
                new CalcResult
                {
                    CalcResultId = 1,
                    CalculationId = "CALC001",
                    CalculationName = "Pupil Funding",
                    Value = "1500.50",
                    ExceptionType = null,
                    ExceptionMessage = null,
                    ExceptionStackTrace = null,
                    CalculationType = "Funding",
                    CalculationDataType = "Decimal",
                    ProviderResultId = "PR001"
                },
                new CalcResult
                {
                    CalcResultId = 2,
                    CalculationId = "CALC002",
                    CalculationName = "Teacher Funding",
                    Value = "2500.00",
                    ExceptionType = null,
                    ExceptionMessage = null,
                    ExceptionStackTrace = null,
                    CalculationType = "Funding",
                    CalculationDataType = "Decimal",
                    ProviderResultId = "PR001"
                },
                new CalcResult
                {
                    CalcResultId = 3,
                    CalculationId = "CALC003",
                    CalculationName = "Total Funding",
                    Value = null,
                    ExceptionType = "CalculationException",
                    ExceptionMessage = "Division by zero",
                    ExceptionStackTrace = "Stack trace details...",
                    CalculationType = "Funding",
                    CalculationDataType = "Decimal",
                    ProviderResultId = "PR003"
                }
            };

            _dbContext.CalcResults.AddRange(data);
            await _dbContext.SaveChangesAsync();

            foreach (var entry in _dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
        #endregion
    }
}
