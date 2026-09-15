using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Services.Results.Models;
using CalculateFunding.Services.Results.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CalculateFunding.Services.Results.UnitTests.Repositories
{
    [TestClass]
    public class SqlProvidersWithResultsFeedIteratorTests
    {
        private CfsDbContext _dbContext;

        [TestInitialize]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<CfsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _dbContext = new CfsDbContext(options);
        }

        [TestMethod]
        public async Task ReadNext_ReturnsProviderWithSpecificationAndMapsFundingNames()
        {
            // Arrange
            string providerId = "prov-1";
            string specificationId = "spec-1";

            var fundingStreamEntity = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS", FundingStreamName = "FS Name" };
            var fundingPeriodEntity = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "FP Name" };

            var psr = new ProviderSpecificationResult
            {
                ProviderId = providerId,
                ProviderVersionId = providerId,
                SpecificationId = specificationId,
                SpecificationName = "spec name",
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                FundingPeriodId = fundingPeriodEntity.FundingPeriodId,
                FundingStreamIds = fundingStreamEntity.FundingStreamId,
                FundingPeriodEnd = DateTime.UtcNow.AddDays(10)
            };

            _dbContext.FundingStreams.Add(fundingStreamEntity);
            _dbContext.FundingPeriods.Add(fundingPeriodEntity);
            _dbContext.ProviderSpecificationResults.Add(psr);

            await _dbContext.SaveChangesAsync();

            ICosmosDbFeedIterator iterator = new SqlProvidersWithResultsFeedIterator(
                _dbContext.ProviderSpecificationResults.Where(x => x.SpecificationId == specificationId && !x.IsDeleted),
                _dbContext.FundingStreams,
                _dbContext.FundingPeriods,
                pageSize: 10);

            // Act
            iterator.HasMoreResults.Should().BeTrue();

            var page = await iterator.ReadNext<ProviderWithResultsForSpecifications>(CancellationToken.None);

            // Assert
            page.Should().NotBeNull();
            var providers = page.ToList();
            providers.Should().HaveCount(1);

            var provider = providers.First();
            provider.Provider.Id.Should().Be(providerId);
            provider.Specifications.Should().HaveCount(1);

            var spec = provider.Specifications.First();
            spec.Id.Should().Be(specificationId);
            spec.Name.Should().Be(psr.SpecificationName);
            spec.FundingPeriodId.Should().Be(fundingPeriodEntity.FundingPeriodName);
            spec.FundingStreamIds.Should().Contain(fundingStreamEntity.FundingStreamName);

            iterator.HasMoreResults.Should().BeFalse();
        }

        [TestMethod]
        public async Task ReadNext_NoResults_ReturnsEmptyAndHasNoMoreResults()
        {
            // Arrange
            string specificationId = "spec-empty";

            var fundingStream = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS", FundingStreamName = "FS" };
            var fundingPeriod = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "FP" };

            _dbContext.FundingStreams.Add(fundingStream);
            _dbContext.FundingPeriods.Add(fundingPeriod);
            await _dbContext.SaveChangesAsync();

            ICosmosDbFeedIterator iterator = new SqlProvidersWithResultsFeedIterator(
                _dbContext.ProviderSpecificationResults.Where(x => x.SpecificationId == specificationId && !x.IsDeleted),
                _dbContext.FundingStreams,
                _dbContext.FundingPeriods,
                pageSize: 10);

            // Act
            iterator.HasMoreResults.Should().BeTrue();

            var page = (await iterator.ReadNext<ProviderWithResultsForSpecifications>(CancellationToken.None)).ToList();

            // Assert
            page.Should().BeEmpty();
            iterator.HasMoreResults.Should().BeFalse();
        }

        [TestMethod]
        public async Task ReadNext_CancellationTokenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            string specificationId = "spec-cancel";

            var fundingStream = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS", FundingStreamName = "FS" };
            var fundingPeriod = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "FP" };

            _dbContext.FundingStreams.Add(fundingStream);
            _dbContext.FundingPeriods.Add(fundingPeriod);
            await _dbContext.SaveChangesAsync();

            ICosmosDbFeedIterator iterator = new SqlProvidersWithResultsFeedIterator(
                _dbContext.ProviderSpecificationResults.Where(x => x.SpecificationId == specificationId && !x.IsDeleted),
                _dbContext.FundingStreams,
                _dbContext.FundingPeriods,
                pageSize: 10);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act / Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            {
                await iterator.ReadNext<ProviderWithResultsForSpecifications>(cts.Token);
            });
        }

        [TestMethod]
        public async Task ReadNext_PagesAcrossMultipleResults()
        {
            // Arrange
            string specificationId = "spec-paging";

            var fundingStream = new FundingStream { FundingStreamId = 1, FundingStreamCode = "FS", FundingStreamName = "FS" };
            var fundingPeriod = new FundingPeriod { FundingPeriodId = 1, FundingPeriodCode = "FP", FundingPeriodName = "FP" };

            _dbContext.FundingStreams.Add(fundingStream);
            _dbContext.FundingPeriods.Add(fundingPeriod);

            for (int i = 1; i <= 3; i++)
            {
                _dbContext.ProviderSpecificationResults.Add(new ProviderSpecificationResult
                {
                    ProviderId = "prov-" + i,
                    ProviderVersionId = "prov-" + i,
                    SpecificationId = specificationId,
                    SpecificationName = "spec " + i,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    FundingPeriodId = fundingPeriod.FundingPeriodId,
                    FundingStreamIds = fundingStream.FundingStreamId,
                    FundingPeriodEnd = DateTime.UtcNow.AddDays(1)
                });
            }

            await _dbContext.SaveChangesAsync();

            ICosmosDbFeedIterator iterator = new SqlProvidersWithResultsFeedIterator(
                _dbContext.ProviderSpecificationResults.Where(x => x.SpecificationId == specificationId && !x.IsDeleted),
                _dbContext.FundingStreams,
                _dbContext.FundingPeriods,
                pageSize: 2);

            // Act & Assert - first page
            iterator.HasMoreResults.Should().BeTrue();
            var firstPage = (await iterator.ReadNext<ProviderWithResultsForSpecifications>(CancellationToken.None)).ToList();
            firstPage.Should().HaveCount(2);
            iterator.HasMoreResults.Should().BeTrue();

            // second page
            var secondPage = (await iterator.ReadNext<ProviderWithResultsForSpecifications>(CancellationToken.None)).ToList();
            secondPage.Should().HaveCount(1);
            iterator.HasMoreResults.Should().BeFalse();
        }
    }
}
