using CalculateFunding.Common.ApiClient.Profiling.Models;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Variations.Changes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;
using System;

namespace CalculateFunding.Services.Publishing.UnitTests.Variations.Changes
{
    public class ClosedProviderReProfilingVariationChangeTests : ReProfilingVariationChangeTestsBase
    {
        protected override string Strategy => "ClosedProviderReProfiling";

        protected override string ChangeName => "Closed provider re-profile variation change";

        protected virtual MidYearType MidYearTypeValue => MidYearType.Closure;

        protected virtual PublishedProviderVersion ReProfilePublishedProvider => PriorState;

        [TestInitialize]
        public virtual void SetUp()
        {
            Change = new ClosedProviderReProfileVariationChange(VariationContext, Strategy);
        }

        protected override void AndTheTheReProfileRequest(FundingLine fundingLine,
            ReProfileRequest reProfileRequest,
            string key = null,
            string eTag = "ETag",
            int variationPointerIndex = 1)
        {
            PublishedProviderVersion publishedProvider = VariationContext.RefreshState;

            string profilePatternKey = publishedProvider.ProfilePatternKeys?.SingleOrDefault(_ => _.FundingLineCode == fundingLine.FundingLineCode)?.Key;

            VariationContext.ProfilePatterns = (VariationContext.ProfilePatterns?.Values ?? ArraySegment<FundingStreamPeriodProfilePattern>.Empty)
                .Concat(new[] {
                    new FundingStreamPeriodProfilePattern {
                    FundingLineId = fundingLine.FundingLineCode,
                    ProfilePatternKey = profilePatternKey,
                    ETag = eTag
                }
            }).ToDictionary(_ => string.IsNullOrWhiteSpace(_.ProfilePatternKey) ? _.FundingLineId : $"{_.FundingLineId}-{_.ProfilePatternKey}");

            ReProfileAudit reProfileAudit = new ReProfileAudit
            {
                FundingLineCode = fundingLine.FundingLineCode,
                VariationPointerIndex = variationPointerIndex
            };

            publishedProvider.AddOrUpdateReProfileAudit(reProfileAudit);

            ReProfileRequestBuilder.Setup(_ => _.BuildReProfileRequest(fundingLine.FundingLineCode,
                    key,
                    It.Is<PublishedProviderVersion>(_ => _ == null || _.PublishedProviderId == ReProfilePublishedProvider.PublishedProviderId),
                    fundingLine.Value,
                    reProfileAudit,
                    MidYearTypeValue,
                    false,
                    false,
                    It.IsAny<Func<string, string, ReProfileAudit, int, bool>>()))
                .ReturnsAsync((reProfileRequest, ((MidYearReProfileVariationChange)Change).ReProfileForSameAmountFunc(fundingLine.FundingLineCode, publishedProvider.ProfilePatternKeys?.SingleOrDefault(_ => _.FundingLineCode == fundingLine.FundingLineCode)?.Key, reProfileAudit, reProfileRequest.VariationPointerIndex ?? 2)));
        }

        protected override void AndTheAffectedFundingLineCodes(params string[] fundingLineCodes)
            => fundingLineCodes.ForEach(_ => VariationContext.AddAffectedFundingLineCode(Strategy, _));

    }
}
