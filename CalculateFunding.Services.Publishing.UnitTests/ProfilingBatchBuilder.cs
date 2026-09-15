using CalculateFunding.Models.Publishing;
using CalculateFunding.Tests.Common.Helpers;
using System;

namespace CalculateFunding.Services.Publishing.UnitTests
{
    public class ProfilingBatchBuilder : TestEntityBuilder
    {
        private FundingLine[] _fundingLines;
        private string _providerType;
        private string _providerSubType;
        private PublishedProviderVersion[] _publishedProviders;
        private decimal _fundingValue;
        private string _fundingLineCode;
        private DateTimeOffset? _dateOpened;
        private string _reasonEstablishmentOpened;
        private string _fundingPeriodId;
        private string _fundingStreamId;
        private string _profilePatternKey;

        public ProfilingBatchBuilder WithFundingLines(params FundingLine[] fundingLines)
        {
            _fundingLines = fundingLines;

            return this;
        }

        public ProfilingBatchBuilder WithProviderType(string providerType)
        {
            _providerType = providerType;

            return this;
        }

        public ProfilingBatchBuilder WithProviderSubType(string providerSubType)
        {
            _providerSubType = providerSubType;

            return this;
        }

        public ProfilingBatchBuilder WithPublishedProviders(params PublishedProviderVersion[] publishedProviders)
        {
            _publishedProviders = publishedProviders;

            return this;
        }

        public ProfilingBatchBuilder WithFundingValue(decimal fundingValue)
        {
            _fundingValue = fundingValue;

            return this;
        }

        public ProfilingBatchBuilder WithFundingLineCode(string fundingLineCode)
        {
            _fundingLineCode = fundingLineCode;

            return this;
        }

        public ProfilingBatchBuilder WithFundingPeriodId(string fundingPeriodId)
        {
            _fundingPeriodId = fundingPeriodId;

            return this;
        }

        public ProfilingBatchBuilder WithFundingStreamId(string fundingStreamId)
        {
            _fundingStreamId = fundingStreamId;

            return this;
        }

        public ProfilingBatchBuilder WithProfilePatternKey(string profilePatternKey)
        {
            _profilePatternKey = profilePatternKey;

            return this;
        }

        public ProfilingBatchBuilder WithDateOpened(DateTimeOffset? dateOpened)
        {
            _dateOpened = dateOpened;

            return this;
        }

        public ProfilingBatchBuilder WithReasonEstablishementOpened(string reasonEstablishmentOpened)
        {
            _reasonEstablishmentOpened = reasonEstablishmentOpened;

            return this;
        }


        public ProfilingBatch Build() =>
            new ProfilingBatch
            {
                FundingLines = _fundingLines,
                ProviderType = _providerType,
                ProviderSubType = _providerSubType,
                PublishedProviders = _publishedProviders,
                FundingValue = _fundingValue,
                FundingLineCode = _fundingLineCode,
                DateOpened = _dateOpened,
                ReasonEstablishmentOpened = _reasonEstablishmentOpened,
                FundingPeriodId = _fundingPeriodId,
                FundingStreamId = _fundingStreamId,
                ProfilePatternKey = _profilePatternKey
            };
    }
}