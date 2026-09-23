using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.TemplateMetadata.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Interfaces;
using CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Model;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels;
using CalculateFunding.Services.Publishing.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling
{
    public class ExistingPublishedFundingDataBackfillings : IExistingPublishedFundingDataBackfilling
    {
        private readonly IReleaseManagementRepository _repo;
        private readonly ILogger _logger;
        private readonly IPublishedFundingContentsChannelPersistenceService _publishedFundingContentsChannelPersistenceService;
        private readonly IPublishedFundingRepository _publishedFundingRepository;
        private readonly IPoliciesService _policiesService;
        private readonly IPublishedFundingContentsGeneratorResolver _publishedFundingContentsGeneratorResolver;
        public ExistingPublishedFundingDataBackfillings(
            ILogger logger,
            IReleaseManagementRepository repo,
            IPublishedFundingContentsChannelPersistenceService publishedFundingContentsChannelPersistenceService,
            IPublishedFundingRepository publishedFundingRepository,
            IPoliciesService policiesService,
            IPublishedFundingContentsGeneratorResolver publishedFundingContentsGeneratorResolver) 
        {
            Guard.ArgumentNotNull(repo, nameof(repo));
            Guard.ArgumentNotNull(publishedFundingContentsChannelPersistenceService, nameof(publishedFundingContentsChannelPersistenceService));
            Guard.ArgumentNotNull(publishedFundingRepository, nameof(publishedFundingRepository));
            Guard.ArgumentNotNull(policiesService, nameof(policiesService));
            Guard.ArgumentNotNull(publishedFundingContentsGeneratorResolver, nameof(publishedFundingContentsGeneratorResolver));

            _logger = logger;
            _repo = repo;
            _publishedFundingContentsChannelPersistenceService = publishedFundingContentsChannelPersistenceService;
            _publishedFundingRepository = publishedFundingRepository;
            _policiesService = policiesService;
            _publishedFundingContentsGeneratorResolver = publishedFundingContentsGeneratorResolver;
        }


        public async Task UpdateMissedProvidersInFundingGroup(List<PublishedProvider> commonProvidersInSameOrgGroup,
             SpecificationSummary specification, IEnumerable<Channel> allChannels, string selectedChannelCode)
        {

            Dictionary<string, List<PublishedProvider>> commonProvidersInSameOrgGroupDict = commonProvidersInSameOrgGroup.GroupBy(obj => obj.Released.Provider.PaymentOrganisationIdentifier)
                      .ToDictionary(group => group.Key, group => group.ToList());

            _logger.Information("ProviderGroupingDataBackfillingService: Existing published funding group count {count}", commonProvidersInSameOrgGroupDict.Keys.Count());

            Channel selectedChannel = allChannels.Where(_ => _.ChannelCode.Equals(selectedChannelCode)).FirstOrDefault();

            IEnumerable<LatestFundingGroupVersionInProviderChannel> fundingGroupVersions = _repo.GetLatestFundingGroupVersion(specification.Id).Result;
            _logger.Information("ProviderGroupingDataBackfillingService: Retrieved LatestProviderVersionInFundingGroup count - {count} ", fundingGroupVersions.Count());

            IEnumerable<ReleasedProviderVersionChannelData> releasedProviderVersionChannels = _repo.GetReleasedProviderVersionChannel(specification.Id, selectedChannel.ChannelId).Result;

            TemplateMetadataContents templateMetadataContents = await ReadTemplateMetadataContents(specification);

            IPublishedFundingContentsGenerator generator = _publishedFundingContentsGeneratorResolver.GetService(templateMetadataContents.SchemaVersion);

            foreach (string orgGroupIdentifierValue in commonProvidersInSameOrgGroupDict.Keys)
            {
                IEnumerable<LatestFundingGroupVersionInProviderChannel> fgvs = fundingGroupVersions
                    .Where(_ => _.OrganisationGroupIdentifierValue.Equals(orgGroupIdentifierValue));

                int selectedChannelId = allChannels.Where(_ => _.ChannelCode.Equals(selectedChannelCode)).FirstOrDefault().ChannelId;

                LatestFundingGroupVersionInProviderChannel fgv = fgvs.Where(_=>_.ChannelId == selectedChannelId).OrderByDescending(_=>_.MajorVersion).FirstOrDefault();

                string cosmosParitionKey = $"funding-{specification.FundingStreams.First().Id}-{specification.FundingPeriod.Id}-{fgv.GroupingReasonCode}-{fgv.OrganisationGroupTypeCode}-{orgGroupIdentifierValue}";

                PublishedFunding publishedFundingCosmosDoc = await _publishedFundingRepository.GetPublishedFundingById(cosmosParitionKey, cosmosParitionKey);

                if (publishedFundingCosmosDoc == null)
                {
                    _logger.Information("ProviderGroupingDataBackfillingService: Retrieved publishedFundingCosmosDoc - {id}", publishedFundingCosmosDoc.Id);
                    continue;
                }

                //find the missing providers
                IEnumerable<string> missingProviderFundingIds = GetMissingProviders(publishedFundingCosmosDoc.Current.ProviderFundings, fgvs.Where(_ => _.ChannelId.Equals(selectedChannel.ChannelId)).Select(_ => _.ProviderFundingId).ToList());

                List<FundingGroupProvider> fundingGroupProviders = new List<FundingGroupProvider>();
                missingProviderFundingIds.ForEach(_ => {
                    fundingGroupProviders.Add(
                        new FundingGroupProvider()
                        {
                            FundingGroupProviderId = Guid.NewGuid(),
                            FundingGroupVersionId = fgv.FundingGroupVersionId,
                            ReleasedProviderVersionChannelId = releasedProviderVersionChannels.Where(f => f.FundingId.Equals(_)).FirstOrDefault().ReleasedProviderVersionChannelId
                        });
                });


                PublishedFundingVersion publishedFundingVersionCosmosDoc = await _publishedFundingRepository.GetPublishedFundingVersionById(publishedFundingCosmosDoc.Current.Id, publishedFundingCosmosDoc.ParitionKey);
                publishedFundingVersionCosmosDoc.ChannelVersions = new List<ChannelVersion>();
                allChannels.ForEach(c =>
                {
                    publishedFundingVersionCosmosDoc.ChannelVersions.Add(new ChannelVersion
                    {
                        type = c.ChannelName,
                        value = Convert.ToInt16(fgvs.Where(_ => _.ChannelId.Equals(c.ChannelId)).FirstOrDefault()?.ChannelVersion)
                    });
                });
                publishedFundingVersionCosmosDoc.FundingId = fgv.GroupFundingId;
                publishedFundingVersionCosmosDoc.MajorVersion = fgv.MajorVersion;
                publishedFundingVersionCosmosDoc.StatusChangedDate = GetDateTimeWithZ(fgv.StatusChangedDate);
                publishedFundingVersionCosmosDoc.EarliestPaymentAvailableDate = GetDateTimeWithZ(fgv.EarliestPaymentAvailableDate);
                publishedFundingVersionCosmosDoc.ExternalPublicationDate = GetDateTimeWithZ(fgv.ExternalPublicationDate);

                string contents = generator.GenerateContents(publishedFundingVersionCosmosDoc, templateMetadataContents);

                if (string.IsNullOrWhiteSpace(contents))
                {
                    throw new RetriableException($"ProviderGroupingDataBackfillingService: Generator failed to generate content for published provider version with id: '{publishedFundingCosmosDoc.Id}'");
                }

                string blobName = GetBlobName(selectedChannelCode, fgv.GroupFundingId);

                _repo.InitialiseTransaction();
                try
                {
                    //inserting the FundingGroupVersion to the RMDB
                    if (_repo.IsExistsFundingGroupVersion(fgv.GroupFundingId).Result)
                    {
                        _logger.Information("ProviderGroupingDataBackfillingService: SQL FGV exists with id - {id}", fgv.GroupFundingId);
                        await _repo.UpdateFundingGroupVersionWithTotalFunding(fgv.GroupFundingId, publishedFundingVersionCosmosDoc.TotalFunding);
                        _logger.Information("ProviderGroupingDataBackfillingService: Successfully updated SQL with total funding");
                        foreach(FundingGroupProvider fgp in fundingGroupProviders)
                        {                                
                            await _repo.CreateFundingGroupProviderUsingAmbientTransaction(fgp);
                            _logger.Information("ProviderGroupingDataBackfillingService: Inserted successfully FundingGroupPrvider table with Id {id}", fgp.FundingGroupProviderId);
                        }
                    }
                    //Inserting the blob 
                    if (_publishedFundingContentsChannelPersistenceService.IsBlobExists(blobName).Result)
                    {
                        _logger.Information("ProviderGroupingDataBackfillingService: Blob exists with name - {name}", blobName);
                        await _publishedFundingContentsChannelPersistenceService.UploadBlobAsync(blobName, contents, specification.Id);
                        _logger.Information("ProviderGroupingDataBackfillingService: Successfully updated the with name - {name}", blobName);
                    }
                    _repo.Commit();
                }
                catch (Exception ex)
                {
                    _repo.RollBack();
                    _logger.Error(ex, "Error in provider grouping data backfilling job for existing items for published group document id - {id} ", publishedFundingCosmosDoc.Id);
                    throw;
                }
                

            }

        }

        #region Private methods

        private static DateTime GetDateTimeWithZ(DateTime inputDateTime)
        {
            return new DateTime(inputDateTime.Ticks, DateTimeKind.Utc);
        }
        private IEnumerable<string> GetMissingProviders(IEnumerable<string> providersInCosmos, IEnumerable<string> providersInSQL)
        {
            return providersInCosmos.Except(providersInSQL).ToList();
        }
        private async Task<TemplateMetadataContents> ReadTemplateMetadataContents(SpecificationSummary specification)
        {
            string fundingStreamId = specification.FundingStreams.First().Id;
            TemplateMetadataContents templateMetadataContents =
                await _policiesService.GetTemplateMetadataContents(fundingStreamId, specification.FundingPeriod.Id, specification.TemplateIds[fundingStreamId]);

            if (templateMetadataContents == null)
            {
                throw new NonRetriableException($"Unable to get template metadata contents for funding stream. '{fundingStreamId}'");
            }

            return templateMetadataContents;
        }

        private string GetBlobName(string channelCode, string fundingId)
        {
            return $"{channelCode}/{fundingId}.json";
        }

        #endregion
    }
}
