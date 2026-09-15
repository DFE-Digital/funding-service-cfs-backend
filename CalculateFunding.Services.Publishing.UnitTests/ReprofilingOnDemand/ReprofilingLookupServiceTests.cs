using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Profiling.Tests.TestHelpers;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.ReprofilingOnDemand;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.UnitTests.ReprofilingOnDemand
{
    [TestClass]
    public class ReprofilingLookupServiceTests
    {
        private string _specificationId;
        private string[] _pageOne;
        private string[] _pageTwo;
        private string[] _pageThree;
        private string[] _publishedProviderIds;
        private SpecificationSummary _specificationSummary;
        private PublishedProviderLookupServiceForReprofiling _publishedProviderLookupService;

        private Mock<IPublishedFundingBulkRepository> _publishedFundingBulkRepo;

        [TestInitialize]
        public void SetUp()
        {
            _publishedFundingBulkRepo = new Mock<IPublishedFundingBulkRepository>();

            _specificationId = NewRandomString();
            _specificationSummary = NewSpecificationSummary(_ => _.WithId(_specificationId));

            _publishedProviderIds = new string[] { "GAG-AC-2223-10084305" };
            _publishedProviderLookupService = new PublishedProviderLookupServiceForReprofiling(_publishedFundingBulkRepo.Object);
        }

        /// <summary>
        /// Empty Published Providers
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task LookupPublishedProviderSummariesFailed()
        {

            IEnumerable<PublishedProvider> publishedProviderFundingSummaries = await WhenTheReprofilingSummaryAreRequestedWithEmptyProviders();

            publishedProviderFundingSummaries.Should().BeNullOrEmpty();
        }


        private async Task<IEnumerable<PublishedProvider>> WhenTheReprofilingSummaryAreRequestedWithEmptyProviders()
            => await _publishedProviderLookupService.GetPublishedProviderReprofilingSummaries(
                 _specificationSummary,
                 null);

        /// <summary>
        /// 1 Published Providers
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task LookupPublishedProviderSummaries()
        {

            IEnumerable<PublishedProvider> publishedProviderFundingSummaries = await WhenTheReprofilingSummaryAreRequested();

            publishedProviderFundingSummaries.Count().Should().Be(0);
        }


        private async Task<IEnumerable<PublishedProvider>> WhenTheReprofilingSummaryAreRequested()
            => await _publishedProviderLookupService.GetPublishedProviderReprofilingSummaries(
                 _specificationSummary,
                 _publishedProviderIds);

     

        private string[] Join(params string[][] pages) => pages.SelectMany(_ => _).ToArray();

        private IEnumerable<string> NewRandomPublishedProviderIdsPage()
        {
            for (int id = 0; id < 100; id++)
            {
                yield return NewRandomString();
            }
        }

        private string NewRandomString() => new RandomString();


        private Provider NewProvider(Action<ProviderBuilder> setUp = null)
        {
            ProviderBuilder builder = new ProviderBuilder();

            setUp?.Invoke(builder);

            return builder.Build();
        }

        private SpecificationSummary NewSpecificationSummary(Action<SpecificationSummaryBuilder> setUp = null)
        {
            SpecificationSummaryBuilder builder = new SpecificationSummaryBuilder();

            string[] fundingStreamIds = new string[1] { NewRandomString() };
            builder.WithFundingStreamIds(fundingStreamIds);

            setUp?.Invoke(builder);

            return builder.Build();
        }
    }
}
