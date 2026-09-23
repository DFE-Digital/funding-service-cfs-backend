using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CalculateFunding.Common.EfCore.GenericRepository;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Services.Profiling.Models;
using CalculateFunding.Services.Profiling.Repositories;
using CalculateFunding.Services.Profiling.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Profiling.Tests
{
    [TestClass]
    public class ProfilePatternRepositoryTests
    {
        private Mock<IUnitOfWork> _uow;
        private EntityModel.CfsDbContext _dbContext;
        private GenericRepository<EntityModel.FundingPeriod> _fundingPeriodRepo;
        private GenericRepository<EntityModel.FundingStream> _fundingStreamRepo;
        private GenericRepository<EntityModel.FundingStreamPeriodProfilePattern> _profilePatternRepo;
        private GenericRepository<EntityModel.ProfilePatternPeriod> _profilePatternPeriodRepo;
        private GenericRepository<EntityModel.ProviderTypeSubType> _providerTypeSubTypeRepo;
        private GenericRepository<EntityModel.ReProfilingConfigurationProperty> _reProfilingConfigurationPropertyRepo;
        private ProfilePatternRepository _repository;

        [TestInitialize]
        public void SetUp()
        {
            _uow = new Mock<IUnitOfWork>();
            var options = new DbContextOptionsBuilder<EntityModel.CfsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _dbContext = new EntityModel.CfsDbContext(options);
            _fundingPeriodRepo = new GenericRepository<EntityModel.FundingPeriod>(_dbContext);
            _fundingStreamRepo = new GenericRepository<EntityModel.FundingStream>(_dbContext);
            _profilePatternRepo = new GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>(_dbContext);
            _profilePatternPeriodRepo = new GenericRepository<EntityModel.ProfilePatternPeriod>(_dbContext);
            _providerTypeSubTypeRepo = new GenericRepository<EntityModel.ProviderTypeSubType>(_dbContext);
            _reProfilingConfigurationPropertyRepo = new GenericRepository<EntityModel.ReProfilingConfigurationProperty>(_dbContext);

            _uow.SetupGet(u => u.context).Returns(_dbContext);
            _uow.Setup(u => u.GenericRepository<EntityModel.FundingPeriod>()).Returns(_fundingPeriodRepo);
            _uow.Setup(u => u.GenericRepository<EntityModel.FundingStream>()).Returns(_fundingStreamRepo);
            _uow.Setup(u => u.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>()).Returns(_profilePatternRepo);
            _uow.Setup(u => u.GenericRepository<EntityModel.ProfilePatternPeriod>()).Returns(_profilePatternPeriodRepo);
            _uow.Setup(u => u.GenericRepository<EntityModel.ProviderTypeSubType>()).Returns(_providerTypeSubTypeRepo);
            _uow.Setup(u => u.GenericRepository<EntityModel.ReProfilingConfigurationProperty>()).Returns(_reProfilingConfigurationPropertyRepo);
            _uow.Setup(u => u.CommitAsync()).Returns(() => _dbContext.SaveChangesAsync());

            _repository = new ProfilePatternRepository(_uow.Object);
        }

        [TestMethod]
        public async Task DeleteProfilePattern_ReturnsNotFound_WhenPatternMissing()
        {
            string id = NewRandomString();

            HttpStatusCode statusCode = await _repository.DeleteProfilePattern(id);

            statusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [TestMethod]
        public async Task DeleteProfilePattern_MarksPatternDeleted_AndCommits()
        {
            string id = NewRandomString();
            var pattern = NewProfilePatternEntity(id);

            _dbContext.FundingStreamPeriodProfilePatterns.Add(pattern);
            _dbContext.SaveChanges();

            DetachAllDBEntries();

            HttpStatusCode statusCode = await _repository.DeleteProfilePattern(id);

            statusCode.Should().Be(HttpStatusCode.NoContent);
            _dbContext.FundingStreamPeriodProfilePatterns.First().IsDeleted.Should().BeTrue();
            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [TestMethod]
        public async Task GetProfilePattern_ByKey_ReturnsNull_WhenFundingPeriodMissing()
        {
            string fundingPeriodId = NewRandomString();
            string fundingStreamId = NewRandomString();

            _dbContext.FundingStreams.Add(NewFundingStream(fundingStreamId));
            _dbContext.SaveChanges();

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriodId, fundingStreamId, NewRandomString(), NewRandomString());

            result.Should().BeNull();
        }

        [TestMethod]
        public async Task GetProfilePattern_ByKey_ReturnsNull_WhenPatternMissing()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriod.FundingPeriodCode, fundingStream.FundingStreamCode, NewRandomString(), NewRandomString());

            result.Should().BeNull();
        }

        [TestMethod]
        public async Task GetProfilePattern_ByKey_ReturnsMappedPattern_WhenFound()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();
            var patternEntity = NewProfilePatternEntity(NewRandomString(), fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);
            var periods = new[]
            {
                NewProfilePatternPeriod(patternEntity.Id, 2024, 1),
                NewProfilePatternPeriod(patternEntity.Id, 2025, 2)
            };
            var providerTypes = new[]
            {
                NewProviderTypeSubType(patternEntity.Id)
            };
            var reprofilingProperties = new[]
            {
                NewReProfilingProperty(patternEntity.Id, nameof(ProfilePatternReProfilingConfiguration.ReProfilingEnabled), "true"),
                NewReProfilingProperty(patternEntity.Id, nameof(ProfilePatternReProfilingConfiguration.IncreasedAmountStrategyKey), "inc")
            };

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);
            _dbContext.FundingStreamPeriodProfilePatterns.Add(patternEntity);
            GivenProfilePatternRelations(periods, providerTypes, reprofilingProperties);

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriod.FundingPeriodCode, fundingStream.FundingStreamCode, patternEntity.FundingLineId, patternEntity.ProfilePatternKey);

            result.Should().NotBeNull();
            result.FundingPeriodId.Should().Be(fundingPeriod.FundingPeriodCode);
            result.FundingStreamId.Should().Be(fundingStream.FundingStreamCode);
            result.FundingLineId.Should().Be(patternEntity.FundingLineId);
            result.ProfilePatternKey.Should().Be(patternEntity.ProfilePatternKey);
            result.ProfilePattern.Should().HaveCount(2);
            result.ProviderTypeSubTypes.Should().HaveCount(1);
            result.ReProfilingConfiguration.ReProfilingEnabled.Should().BeTrue();
            result.ReProfilingConfiguration.IncreasedAmountStrategyKey.Should().Be("inc");
        }

        [TestMethod]
        public async Task GetProfilePattern_ById_ReturnsNull_WhenPatternMissing()
        {
            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(NewRandomString());

            result.Should().BeNull();
        }

        [TestMethod]
        public async Task GetProfilePattern_ById_ReturnsMappedPattern_WhenFound()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();
            var patternEntity = NewProfilePatternEntity(NewRandomString(), fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);
            var periods = new[] { NewProfilePatternPeriod(patternEntity.Id, 2024, 1) };
            var providerTypes = new[] { NewProviderTypeSubType(patternEntity.Id) };

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);
            _dbContext.FundingStreamPeriodProfilePatterns.Add(patternEntity);
            GivenProfilePatternRelations(periods, providerTypes, Array.Empty<EntityModel.ReProfilingConfigurationProperty>());

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(patternEntity.Id);

            result.Should().NotBeNull();
            result.FundingPeriodId.Should().Be(fundingPeriod.FundingPeriodCode);
            result.FundingStreamId.Should().Be(fundingStream.FundingStreamCode);
        }

        [TestMethod]
        public async Task GetProfilePattern_ByProviderTypeSubType_ReturnsNull_WhenNoPatterns()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriod.FundingPeriodCode, fundingStream.FundingStreamCode, NewRandomString(), "type", "subtype");

            result.Should().BeNull();
        }

        [TestMethod]
        public async Task GetProfilePattern_ByProviderTypeSubType_ReturnsMappedPattern_WhenMatchFound()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();
            var patternEntity = NewProfilePatternEntity(NewRandomString(), fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);
            var providerType = "type";
            var providerSubType = "subtype";
            var providerTypes = new[] { new EntityModel.ProviderTypeSubType { FundingStreamPeriodProfilePatternId = patternEntity.Id, ProviderType = providerType, ProviderSubType = providerSubType } };

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);
            _dbContext.FundingStreamPeriodProfilePatterns.Add(patternEntity);
            GivenProfilePatternRelations(Array.Empty<EntityModel.ProfilePatternPeriod>(), providerTypes, Array.Empty<EntityModel.ReProfilingConfigurationProperty>());

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriod.FundingPeriodCode, fundingStream.FundingStreamCode, patternEntity.FundingLineId, providerType, providerSubType);

            result.Should().NotBeNull();
            result.ProviderTypeSubTypes.Should().ContainSingle();
        }

        [TestMethod]
        public async Task GetProfilePattern_ByProviderTypeDateOpened_ReturnsNull_WhenNoPatterns()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriod.FundingPeriodCode, fundingStream.FundingStreamCode, NewRandomString(), "type", DateTimeOffset.UtcNow, NewRandomString());

            result.Should().BeNull();
        }

        [TestMethod]
        public async Task GetProfilePattern_ByProviderTypeDateOpened_ReturnsMappedPattern_WhenOpenDateMatches()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();
            var patternEntity = NewProfilePatternEntity(NewRandomString(), fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);
            var providerType = "type";
            var dateOpened = DateTimeOffset.UtcNow.Date;
            var reasonEstblishmentOpened = NewRandomString();
            patternEntity.ProfilePatternOpenDateConfig = JsonConvert.SerializeObject(new[]
            {
                new ProfilePatternOpenDateConfiguration
                {
                    ProviderType = providerType,
                    OpenDateStart = dateOpened.AddDays(-1),
                    OpenDateEnd = dateOpened.AddDays(1),
                    OpenReason = new string[]{ reasonEstblishmentOpened }
                }
            });

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);
            _dbContext.FundingStreamPeriodProfilePatterns.Add(patternEntity);
            GivenProfilePatternRelations(Array.Empty<EntityModel.ProfilePatternPeriod>(), Array.Empty<EntityModel.ProviderTypeSubType>(), Array.Empty<EntityModel.ReProfilingConfigurationProperty>());

            FundingStreamPeriodProfilePattern result = await _repository.GetProfilePattern(fundingPeriod.FundingPeriodCode, fundingStream.FundingStreamCode, patternEntity.FundingLineId, providerType, dateOpened, reasonEstblishmentOpened);

            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GetProfilePatternsForFundingStreamAndFundingPeriod_ReturnsEmpty_WhenFundingPeriodMissing()
        {
            _dbContext.FundingStreams.Add(NewFundingStream());
            _dbContext.SaveChanges();

            IEnumerable<FundingStreamPeriodProfilePattern> results = await _repository.GetProfilePatternsForFundingStreamAndFundingPeriod(NewRandomString(), NewRandomString());

            results.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GetProfilePatternsForFundingStreamAndFundingPeriod_ReturnsMappedPatterns_WhenPatternsExist()
        {
            var fundingPeriod = NewFundingPeriod();
            var fundingStream = NewFundingStream();
            var patternOne = NewProfilePatternEntity(NewRandomString(), fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);
            var patternTwo = NewProfilePatternEntity(NewRandomString(), fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);
            _dbContext.FundingStreamPeriodProfilePatterns.AddRange(patternOne, patternTwo);
            _dbContext.SaveChanges();

            FundingStreamPeriodProfilePattern[] results = (await _repository.GetProfilePatternsForFundingStreamAndFundingPeriod(fundingStream.FundingStreamCode, fundingPeriod.FundingPeriodCode)).ToArray();

            results.Should().HaveCount(2);
        }

        [TestMethod]
        public async Task SaveFundingStreamPeriodProfilePattern_ReturnsNotFound_WhenFundingPeriodMissing()
        {
            var profilePattern = NewServiceProfilePattern();

            _dbContext.FundingStreams.Add(NewFundingStream(profilePattern.FundingStreamId));
            _dbContext.SaveChanges();

            HttpStatusCode result = await _repository.SaveFundingStreamPeriodProfilePattern(profilePattern);

            result.Should().Be(HttpStatusCode.NotFound);
        }

        [TestMethod]
        public async Task SaveFundingStreamPeriodProfilePattern_InsertsNewPattern_AndReturnsCreated()
        {
            var profilePattern = NewServiceProfilePattern();
            var fundingPeriod = NewFundingPeriod(profilePattern.FundingPeriodId);
            var fundingStream = NewFundingStream(profilePattern.FundingStreamId);

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);

            HttpStatusCode result = await _repository.SaveFundingStreamPeriodProfilePattern(profilePattern);

            result.Should().Be(HttpStatusCode.OK);
            _dbContext.FundingStreamPeriodProfilePatterns.Should().ContainSingle(p => p.Id == profilePattern.Id);
            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [TestMethod]
        public async Task SaveFundingStreamPeriodProfilePattern_UpdatesExistingPattern_AndReturnsOk()
        {
            var profilePattern = NewServiceProfilePattern();
            var fundingPeriod = NewFundingPeriod(profilePattern.FundingPeriodId);
            var fundingStream = NewFundingStream(profilePattern.FundingStreamId);
            var existingEntity = NewProfilePatternEntity(profilePattern.Id, fundingPeriod.FundingPeriodId, fundingStream.FundingStreamId);

            GivenFundingPeriodAndStream(fundingPeriod, fundingStream);
            _dbContext.FundingStreamPeriodProfilePatterns.Add(existingEntity);
            _dbContext.SaveChanges();

            HttpStatusCode result = await _repository.SaveFundingStreamPeriodProfilePattern(profilePattern);

            result.Should().Be(HttpStatusCode.OK);
            _dbContext.FundingStreamPeriodProfilePatterns.Should().ContainSingle(p => p.Id == profilePattern.Id);
            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        private void GivenFundingPeriodAndStream(EntityModel.FundingPeriod fundingPeriod, EntityModel.FundingStream fundingStream)
        {
            _dbContext.FundingPeriods.Add(fundingPeriod);
            _dbContext.FundingStreams.Add(fundingStream);
            _dbContext.SaveChanges();
        }

        private void GivenProfilePatternRelations(
            IEnumerable<EntityModel.ProfilePatternPeriod> periods,
            IEnumerable<EntityModel.ProviderTypeSubType> providerTypes,
            IEnumerable<EntityModel.ReProfilingConfigurationProperty> reprofilingProperties)
        {
            _dbContext.ProfilePatternPeriods.AddRange(periods);
            _dbContext.ProviderTypeSubTypes.AddRange(providerTypes);
            _dbContext.ReProfilingConfigurationProperties.AddRange(reprofilingProperties);
            _dbContext.SaveChanges();
        }

        private static IQueryable<T> AsAsyncQueryable<T>(params T[] items)
            => new TestAsyncEnumerable<T>(items ?? Array.Empty<T>());

        private static EntityModel.FundingPeriod NewFundingPeriod(string code = null)
            => new EntityModel.FundingPeriod
            {
                FundingPeriodId = 1,
                FundingPeriodCode = code ?? NewRandomString(),
                FundingPeriodName = NewRandomString()
            };

        private static EntityModel.FundingStream NewFundingStream(string code = null)
            => new EntityModel.FundingStream
            {
                FundingStreamId = 1,
                FundingStreamCode = code ?? NewRandomString(),
                FundingStreamName = NewRandomString()
            };

        private static EntityModel.FundingStreamPeriodProfilePattern NewProfilePatternEntity(string id, int fundingPeriodId = 1, int fundingStreamId = 1)
            => new EntityModel.FundingStreamPeriodProfilePattern
            {
                Id = id,
                FundingPeriodId = fundingPeriodId,
                FundingStreamId = fundingStreamId,
                FundingLineId = NewRandomString(),
                ProfilePatternKey = NewRandomString(),
                ProfilePatternType = ProfilePatternType.Percent.ToString(),
                RoundingStrategy = RoundingStrategy.RoundUp.ToString(),
                FundingStreamPeriodStartDate = DateTime.Today.AddDays(-10),
                FundingStreamPeriodEndDate = DateTime.Today.AddDays(10),
                AllowUserToEditProfilePattern = true,
                ProfilePatternDisplayName = "Test Profile Pattern",
                ProfilePatternDescription = "Test Profile Pattern Description"
            };

        private static EntityModel.ProfilePatternPeriod NewProfilePatternPeriod(string profilePatternId, int year, int occurrence)
            => new EntityModel.ProfilePatternPeriod
            {
                FundingStreamPeriodProfilePatternId = profilePatternId,
                PeriodType = PeriodType.CalendarMonth.ToString(),
                Period = "April",
                PeriodStartDate = DateTime.Today.AddDays(-10),
                PeriodEndDate = DateTime.Today.AddDays(-5),
                PeriodYear = year,
                Occurrence = occurrence,
                DistributionPeriod = "April",
                PeriodPatternPercentage = "0.5"
            };

        private static EntityModel.ProviderTypeSubType NewProviderTypeSubType(string profilePatternId)
            => new EntityModel.ProviderTypeSubType
            {
                FundingStreamPeriodProfilePatternId = profilePatternId,
                ProviderType = "type",
                ProviderSubType = "subtype"
            };

        private static EntityModel.ReProfilingConfigurationProperty NewReProfilingProperty(string profilePatternId, string name, string value)
            => new EntityModel.ReProfilingConfigurationProperty
            {
                FundingStreamPeriodProfilePatternId = profilePatternId,
                PropertyName = name,
                PropertyValue = value
            };

        private FundingStreamPeriodProfilePattern NewServiceProfilePattern()
        {
            var configuration = new ProfilePatternReProfilingConfiguration
            {
                ReProfilingEnabled = true,
                IncreasedAmountStrategyKey = "inc"
            };
            var periods = new[]
            {
                new ProfilePeriodPattern(PeriodType.CalendarMonth, "April", DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-5), 2024, 1, "April", 0.5m, null)
            };
            var providerTypes = new[] { new ProviderTypeSubType { ProviderType = "type", ProviderSubType = "subtype" } };
            var profilePattern = new FundingStreamPeriodProfilePatternBuilder()
                .WithPeriods(periods)
                .WithProviderTypeSubTypes(providerTypes)
                .WithReProfilingConfiguration(configuration)
                .Build();

            profilePattern.ProfilePatternType = ProfilePatternType.Percent;
            profilePattern.RoundingStrategy = RoundingStrategy.RoundUp;

            return profilePattern;
        }

        private static string NewRandomString() => new RandomString();

        private class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
        {
            private readonly IQueryProvider _inner;

            public TestAsyncQueryProvider(IQueryProvider inner)
            {
                _inner = inner;
            }

            public IQueryable CreateQuery(Expression expression)
                => new TestAsyncEnumerable<TEntity>(expression);

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
                => new TestAsyncEnumerable<TElement>(expression);

            public object Execute(Expression expression)
                => _inner.Execute(expression);

            public TResult Execute<TResult>(Expression expression)
                => _inner.Execute<TResult>(expression);

            public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
                => Execute<TResult>(expression);
        }

        private class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
        {
            public TestAsyncEnumerable(IEnumerable<T> enumerable)
                : base(enumerable)
            {
            }

            public TestAsyncEnumerable(Expression expression)
                : base(expression)
            {
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

            IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
        }

        private class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> _inner;

            public TestAsyncEnumerator(IEnumerator<T> inner)
            {
                _inner = inner;
            }

            public T Current => _inner.Current;

            public ValueTask DisposeAsync()
            {
                _inner.Dispose();
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> MoveNextAsync()
                => new ValueTask<bool>(_inner.MoveNext());
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
