using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Models.Publishing.Reprofiling;
using CalculateFunding.Services.Core.Interfaces.Threading;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels;
using CalculateFunding.Services.Publishing.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.ReprofilingOnDemand
{
    public class PublishedProviderReprofilingSummaryProcessor : IPublishedProviderReprofilingSummaryProcessor
    {
        private readonly IReleaseManagementRepository _releaseManagementRepository;
        private readonly IPublishedProviderLookupServiceForReprofiling _reprofilingLookupService;

        public PublishedProviderReprofilingSummaryProcessor(IProducerConsumerFactory producerConsumerFactory,
            IReleaseManagementRepository releaseManagementRepository,
            IPublishedProviderLookupServiceForReprofiling reprofilingLookupService)
        {
            Guard.ArgumentNotNull(producerConsumerFactory, nameof(producerConsumerFactory));
            Guard.ArgumentNotNull(releaseManagementRepository, nameof(releaseManagementRepository));
            Guard.ArgumentNotNull(reprofilingLookupService, nameof(reprofilingLookupService));

            _releaseManagementRepository = releaseManagementRepository;
            _reprofilingLookupService = reprofilingLookupService;
        }


        public async Task<IEnumerable<ProviderVersionInChannel>> GetProviderVersionInFundingConfiguration(
            string specificationId,
            FundingConfiguration fundingConfiguration)
        {
            if (fundingConfiguration.ReleaseChannels.IsNullOrEmpty())
            {
                return Enumerable.Empty<ProviderVersionInChannel>();
            }

            IEnumerable<Channel> channels = await GetChannels(fundingConfiguration.ReleaseChannels.Where(rc => rc.IsVisible).Select(_ => _.ChannelCode));

            IEnumerable<ProviderVersionInChannel> latestPublishedProviderVersions =
                await _releaseManagementRepository.GetLatestPublishedProviderVersions(specificationId, channels.Select(_ => _.ChannelId));

            return latestPublishedProviderVersions;
        }

        public async Task<ReprofilingSummaryResult> GetFundingSummaryForReprofilingPublishedProviders(IEnumerable<string> publishedProviderIds,
            SpecificationSummary specificationSummary,
            FundingConfiguration fundingConfiguration,
            Common.ApiClient.Policies.Models.FundingPeriod fundingPeriod,
            IEnumerable<ProfileVariationPointer> currentProfileVariationPointerResult)
        {
            Guard.ArgumentNotNull(specificationSummary, nameof(specificationSummary));
            Guard.ArgumentNotNull(fundingConfiguration, nameof(fundingConfiguration));
            Guard.ArgumentNotNull(publishedProviderIds, nameof(publishedProviderIds));

            IEnumerable<string> channelCodes = new List<String>() { ChannelType.Contracting.ToString(), ChannelType.Payment.ToString(), ChannelType.Statement.ToString() };

            IEnumerable<Channel> channels = await GetChannels(channelCodes.Where(_ => fundingConfiguration.ReleaseChannels.Any(rc => rc.IsVisible && rc.ChannelCode == _)));

            IEnumerable<PublishedProvider> publishedProvidersSummaryList = await _reprofilingLookupService.GetPublishedProviderReprofilingSummaries(
                    specificationSummary,
                    publishedProviderIds );

            IEnumerable<ProviderVersionInChannel> latestPublishedProviderVersions =
                    await _releaseManagementRepository.GetLatestPublishedProviderVersions(specificationSummary.Id, channels.Select(_ => _.ChannelId));


            List<ProviderSummaryResult> providerSummaryResultList = new List<ProviderSummaryResult>();
            foreach(PublishedProvider publishedProvider in publishedProvidersSummaryList)
            {
                ProviderSummaryResult providerSummaryResult = new ProviderSummaryResult()
                {
                    UKPRN = publishedProvider.Current.ProviderId,
                    Name = publishedProvider.Current.Provider.Name,
                    OpenDate = publishedProvider.Current.Provider.DateOpened
                };
                if(publishedProvider.Current.Status == PublishedProviderStatus.Draft)
                {
                    providerSummaryResult.ErrorMessage = "The allocation state state of the provider must either approved, updated or released";
                    providerSummaryResult.IsEligible = false;
                    providerSummaryResultList.Add(providerSummaryResult);
                    continue;
                }
                if (publishedProvider.Current.Provider.Status != "Open")
                {
                    providerSummaryResult.ErrorMessage = "The provider is in an open state only eligible for reprofiling";
                    providerSummaryResult.IsEligible = false;
                    providerSummaryResultList.Add( providerSummaryResult );
                    continue;
                }
                if (publishedProvider.Current.Provider.DateOpened == null || (fundingPeriod.StartDate > publishedProvider.Current.Provider.DateOpened || fundingPeriod.EndDate < publishedProvider.Current.Provider.DateOpened))
                {
                    providerSummaryResult.ErrorMessage = "The provider open date is within the year of the allocation funding year";
                    providerSummaryResult.IsEligible = false;
                    providerSummaryResultList.Add(providerSummaryResult);
                    continue;
                }
                IEnumerable<ProviderVersionInChannel> filteredProviders = latestPublishedProviderVersions.Where(_ => _.ProviderId == publishedProvider.Current.Provider.ProviderId);

                int statementMajorVersion = Convert.ToInt32(filteredProviders.Where(_ => _.ChannelCode == ChannelType.Statement.ToString())?.FirstOrDefault()?.MajorVersion);
                int paymentMajorVersion = Convert.ToInt32(filteredProviders.Where(_ => _.ChannelCode == ChannelType.Payment.ToString())?.FirstOrDefault()?.MajorVersion);
                int contractMajorVersion = Convert.ToInt32(filteredProviders.Where(_ => _.ChannelCode == ChannelType.Contracting.ToString())?.FirstOrDefault()?.MajorVersion);
                if(paymentMajorVersion > 0 || contractMajorVersion > 0)
                {
                    providerSummaryResult.ErrorMessage = "The provider should not release for payment or contract in past";
                    providerSummaryResult.IsEligible = false;
                    providerSummaryResultList.Add(providerSummaryResult);
                    continue;
                }
                if (statementMajorVersion == 0)
                {
                    providerSummaryResult.ErrorMessage = "The provider should previously been released for statement only";
                    providerSummaryResult.IsEligible = false;
                    providerSummaryResultList.Add(providerSummaryResult);
                    continue;
                }
                
                IEnumerable<string> fundingLineCodes = publishedProvider.Current.FundingLines.
                                    Where(_=>_.FundingLineCode != null)
                                    .Select(_ => _.FundingLineCode)
                                    .ToList();

                ProfileVariationPointer highestproviderProfileVariationPointer = currentProfileVariationPointerResult
                        .Where(_ => fundingLineCodes.Contains(_.FundingLineId))
                        .Where(pp => pp.Year != 0 && pp.TypeValue != null)
                        .OrderByDescending(pp => pp.Year)
                        .ThenByDescending(pp => GetMonthOrder(pp.TypeValue))
                        .FirstOrDefault();

                if (highestproviderProfileVariationPointer != null)
                {
                    // Get the DateTimeOffset for highestPeriod
                    DateTimeOffset highestPeriodDate = new DateTimeOffset(new DateTime(highestproviderProfileVariationPointer.Year, GetMonthOrder(highestproviderProfileVariationPointer.TypeValue), 1));

                    // Compare highestPeriodDate with dateOpened
                    int comparisonResult = highestPeriodDate.CompareTo((DateTimeOffset)publishedProvider.Current.Provider.DateOpened);
                    //highestPeriod is earlier than DateOpened.
                    if (comparisonResult < 0)
                    {
                        providerSummaryResult.ErrorMessage = "The period of the provider open date is less than the period of the highest variation pointer setting for any funding line";
                        providerSummaryResult.IsEligible = false;
                        providerSummaryResultList.Add(providerSummaryResult);
                        continue;
                    }
                }
                providerSummaryResult.IsEligible = true;
                providerSummaryResultList.Add(providerSummaryResult);
            }

            ReprofilingSummaryResult reprofilingSummaryResult = new ReprofilingSummaryResult() 
            { 
                TotalProviders = publishedProviderIds.Count(),
                TotalEligibleProviders = providerSummaryResultList.Where(_=>_.IsEligible).Count(),
                ProviderSummaryResult = providerSummaryResultList
            };

            return reprofilingSummaryResult;
        }

        // Helper method to convert month string to a comparable order
        private int GetMonthOrder(string month)
        {
            // You can customize this based on your specific month representation
            switch (month.ToLower())
            {
                case "january": return 1;
                case "february": return 2;
                case "march": return 3;
                case "april": return 4;
                case "may": return 5;
                case "june": return 6;
                case "july": return 7;
                case "august": return 8;
                case "september": return 9;
                case "october": return 10;
                case "november": return 11;
                case "december": return 12;
                default: return 0; // Unknown month
            }
        }

        private async Task<IEnumerable<Channel>> GetChannels(IEnumerable<string> channelCodes)
        {
            List<Channel> channels = new List<Channel>();
            foreach (string channelCode in channelCodes)
            {
                Channel channel = await _releaseManagementRepository.GetChannelByChannelCode(channelCode);
                if (channel == null)
                {
                    throw new KeyNotFoundException($"Channel {channelCode} not found");
                }
                channels.Add(channel);
            }

            return channels;
        }

    }
}
