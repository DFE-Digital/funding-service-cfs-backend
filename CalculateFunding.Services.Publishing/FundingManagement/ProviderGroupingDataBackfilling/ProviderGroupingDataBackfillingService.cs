using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using CalculateFunding.Services.Publishing.Interfaces;
using Serilog;
using System.Threading.Tasks;
using System;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels.QueryResults;
using System.Collections.Generic;
using CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling.Interfaces;
using CalculateFunding.Services.Processing;
using Microsoft.AspNetCore.Mvc;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Services.Core.Constants;
using System.Linq;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using System.Globalization;
using CalculateFunding.Models.Publishing;
using Polly;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels;
using CalculateFunding.Generators.OrganisationGroup.Models;
using CalculateFunding.Services.Publishing.Models;
using Channel = CalculateFunding.Services.Publishing.FundingManagement.SqlModels.Channel;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Services.Core.Interfaces;
using Azure.Messaging.ServiceBus;

namespace CalculateFunding.Services.Publishing.FundingManagement.ProviderGroupingDataBackfilling
{
    public class ProviderGroupingDataBackfillingService : JobProcessingService, IProviderGroupingDataBackfillingService
    {
        private readonly IReleaseManagementRepository _repo;
        private readonly ISpecificationService _specificationService;
        private readonly ILogger _logger;
        private readonly AsyncPolicy _publishingResiliencePolicy;
        private readonly IPublishedFundingDataService _publishedFundingDataService;
        private readonly IPublishedFundingContentsChannelPersistenceService _publishedFundingContentsChannelPersistenceService;
        private readonly IPoliciesService _policiesService;
        private readonly IChannelOrganisationGroupGeneratorService _channelOrganisationGroupGeneratorService;
        private readonly IChannelOrganisationGroupChangeDetector _channelOrganisationGroupChangeDetector;
        private readonly IFundingGroupService _fundingGroupService;
        private readonly IFundingGroupDataGenerator _fundingGroupDataGenerator;
        private readonly IFundingGroupDataPersistenceService _fundingGroupDataPersistenceService;
        private readonly IPublishedProvidersLoadContext _publishProvidersLoadContext;
        private readonly IUniqueIdentifierProvider _fundingGroupIdentifierGenerator;
        private readonly IExistingPublishedFundingDataBackfilling _existingPublishedFundingDataBackfilling;
        public ProviderGroupingDataBackfillingService(
            ISpecificationService specificationService,
            IJobManagement jobManagement,
            ILogger logger,
            IReleaseManagementRepository repo,
            IPublishingResiliencePolicies publishingResiliencePolicies,
            IPublishedFundingDataService publishedFundingDataService,
            IPublishedFundingContentsChannelPersistenceService publishedFundingContentsChannelPersistenceService,
            IPoliciesService policiesService,
            IChannelOrganisationGroupGeneratorService channelOrganisationGroupGeneratorService,
            IChannelOrganisationGroupChangeDetector channelOrganisationGroupChangeDetector,
            IFundingGroupService fundingGroupService,
            IFundingGroupDataGenerator fundingGroupDataGenerator,
            IFundingGroupDataPersistenceService fundingGroupDataPersistenceService,
            IPublishedProvidersLoadContext publishProvidersLoadContext,
            IUniqueIdentifierProvider fundingGroupIdentifierGenerator,
            IExistingPublishedFundingDataBackfilling existingPublishedFundingDataBackfilling) : base(jobManagement, logger)
        {
            Guard.ArgumentNotNull(repo, nameof(repo));
            Guard.ArgumentNotNull(specificationService, nameof(specificationService));
            Guard.ArgumentNotNull(publishedFundingDataService, nameof(publishedFundingDataService));
            Guard.ArgumentNotNull(publishedFundingContentsChannelPersistenceService, nameof(publishedFundingContentsChannelPersistenceService));
            Guard.ArgumentNotNull(policiesService, nameof(policiesService));
            Guard.ArgumentNotNull(channelOrganisationGroupGeneratorService, nameof(channelOrganisationGroupGeneratorService));
            Guard.ArgumentNotNull(channelOrganisationGroupChangeDetector, nameof(channelOrganisationGroupChangeDetector));
            Guard.ArgumentNotNull(fundingGroupService, nameof(fundingGroupService));
            Guard.ArgumentNotNull(fundingGroupDataGenerator, nameof(fundingGroupDataGenerator));
            Guard.ArgumentNotNull(fundingGroupDataPersistenceService, nameof(fundingGroupDataPersistenceService));
            Guard.ArgumentNotNull(publishProvidersLoadContext, nameof(publishProvidersLoadContext));
            Guard.ArgumentNotNull(fundingGroupIdentifierGenerator, nameof(fundingGroupIdentifierGenerator));
            Guard.ArgumentNotNull(existingPublishedFundingDataBackfilling, nameof(existingPublishedFundingDataBackfilling));

            _publishedFundingDataService = publishedFundingDataService;
            _specificationService = specificationService;
            _logger = logger;
            _repo = repo;
            _publishingResiliencePolicy = publishingResiliencePolicies.PublishedFundingRepository;
            _publishedFundingContentsChannelPersistenceService = publishedFundingContentsChannelPersistenceService;
            _policiesService = policiesService;
            _channelOrganisationGroupGeneratorService = channelOrganisationGroupGeneratorService;
            _channelOrganisationGroupChangeDetector = channelOrganisationGroupChangeDetector;
            _fundingGroupService = fundingGroupService;
            _fundingGroupDataGenerator = fundingGroupDataGenerator;
            _fundingGroupDataPersistenceService = fundingGroupDataPersistenceService;
            _publishProvidersLoadContext = publishProvidersLoadContext;
            _fundingGroupIdentifierGenerator = fundingGroupIdentifierGenerator;
            _existingPublishedFundingDataBackfilling = existingPublishedFundingDataBackfilling;
        }


        public async Task<IActionResult> QueueProviderGroupingDataBackfillingJob(Reference author,
            string correlationId,
            string specificationId, string channelCode, string statusChangedDate)
        {
            IEnumerable<JobSummary> jobTypesRunning = await GetJobTypes(new string[] {
                    JobConstants.DefinitionNames.ProviderGroupingDataBackfillingJob
            });

            if (jobTypesRunning.AnyWithNullCheck())
            {
                throw new NonRetriableException($"Unable to queue a new provider grouping data backfilling job as one is already running job id:{jobTypesRunning.First().JobId}.");
            }

            Job job = await QueueJob(new JobCreateModel
            {
                JobDefinitionId = JobConstants.DefinitionNames.ProviderGroupingDataBackfillingJob,
                InvokerUserId = author?.Id,
                InvokerUserDisplayName = author?.Name,
                CorrelationId = correlationId,
                Properties = new Dictionary<string, string>
                {
                    {"specification-id", specificationId},
                    {"channel-code", channelCode.ToString() },
                    {"status-changed-date", statusChangedDate},
                },
                Trigger = new Trigger
                {
                    EntityId = specificationId,
                    EntityType = "Specification"
                }
            });

            return new OkObjectResult(new JobCreationResponse
            {
                JobId = job.Id
            });
        }

        public override async Task Process(ServiceBusReceivedMessage message)
        {
            Guard.ArgumentNotNull(message, nameof(message));

            string jobId = Job?.Id;
            Reference author = message.GetUserDetails();
            string correlationId = message.GetCorrelationId();

            string specificationId = message.GetUserProperty<string>("specification-id");
            string channelCode = message.GetUserProperty<string>("channel-code");
            string statusChangedDate = message.GetUserProperty<string>("status-changed-date");

            await BackfillProviderGroupingData(specificationId, channelCode, statusChangedDate, jobId, correlationId, author);
        }

        private async Task BackfillProviderGroupingData(string specificationId,
            string channelCode, string statusChangedDate, string jobId, string correlationId, Reference author)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(channelCode, nameof(channelCode));
            Guard.ArgumentNotNull(statusChangedDate, nameof(statusChangedDate));

            if (!DateTime.TryParseExact(statusChangedDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedStatusChangedDate))
            {
                throw new InvalidOperationException("ProviderGroupingDataBackfillingService: Date time format is not correct. try dd-MM-yyyy");
            }
            SpecificationSummary specification = await _specificationService.GetSpecificationSummaryById(specificationId);
            if (specification == null)
            {
                throw new InvalidOperationException("ProviderGroupingDataBackfillingService: Specification not found");
            }

            FundingConfiguration fundingConfiguration = await _policiesService.GetFundingConfiguration(specification.FundingStreams.First().Id, specification.FundingPeriod.Id);

            IEnumerable<Channel> allChannels = await _repo.GetChannels();
            if (!allChannels.Any() || !allChannels.Where(_=>_.ChannelCode.Equals(channelCode)).Any())
            {
                throw new InvalidOperationException("ProviderGroupingDataBackfillingService: Channel code not found.");
            }
            //Removing SpecToSpec since its not been handled in RM
            allChannels = allChannels.Where(p => p.ChannelId < (int)ChannelType.SpecToSpec);

            List<PublishedProvider> allPublishedProvidersBatch = (await _publishingResiliencePolicy.ExecuteAsync(() =>
               _publishedFundingDataService.GetReleasedPublishedProviders(specification.FundingStreams.First().Id, specificationId))).ToList();
            
            List<PublishedProvider> publishedProvidersBefore = allPublishedProvidersBatch.Where(_ => _.Released.Date < parsedStatusChangedDate).ToList();
            //We are trying to correct the data which is released before date. If no data available no point of proceeding further.
            if(!publishedProvidersBefore.Any())
            {
                throw new NonRetriableException("ProviderGroupingDataBackfillingService: No data available before the provided date to proceed with");
            }
            List<PublishedProvider> publishedProvidersAfter = allPublishedProvidersBatch.Where(_ => _.Released.Date > parsedStatusChangedDate).ToList();

            _logger.Information("ProviderGroupingDataBackfillingService: No. of providers released before data is {before} and after is {after}", publishedProvidersBefore.Count(), publishedProvidersAfter.Count());

            List<PublishedProvider> commonProvidersInSameOrgGroup = publishedProvidersBefore.Where(b => publishedProvidersAfter.Any(a => a.Released.Provider.PaymentOrganisationIdentifier == b.Released.Provider.PaymentOrganisationIdentifier)).ToList();
            Dictionary<string, PublishedProvider> providersToBeCreated = publishedProvidersBefore.Where(a => !commonProvidersInSameOrgGroup.Any(b => b.Id == a.Id)).ToList().ToDictionary(_ => _.Released.ProviderId, _ => _);

            #region Updating the existing PublishedFunding docs and sql

            _logger.Information("ProviderGroupingDataBackfillingService: starting executing the provider backfilling to existing published funding group with provider count {count}", commonProvidersInSameOrgGroup.Count());

            if (commonProvidersInSameOrgGroup.Any())
            {
                //Existing published funding data will be updated here with the missed provider data.
                await _existingPublishedFundingDataBackfilling.UpdateMissedProvidersInFundingGroup(commonProvidersInSameOrgGroup,
                         specification, allChannels, channelCode);

                _logger.Information("ProviderGroupingDataBackfillingService: Execution completed successfully for the existing published funding data backfilling");
            }
            #endregion

            #region Creating new Grouping data and published funding blob

            _logger.Information("ProviderGroupingDataBackfillingService: '{Count}' number of providers released before the status change date for the creation of new fudingdata", providersToBeCreated.Count());

            if (providersToBeCreated.Any())
            {
                _logger.Information("ProviderGroupingDataBackfillingService: started executing the creation of new funding group data and blob");
                _logger.Information("ProviderGroupingDataBackfillingService: Initialising SQL transaction");
                _repo.InitialiseTransaction();
                try
                {
                    await CreateNewFundingGroupDataAndBlob(specification,
                        fundingConfiguration,
                        channelCode,
                        allChannels,
                        providersToBeCreated,
                        jobId,
                        correlationId,
                        author);

                    _logger.Information("ProviderGroupingDataBackfillingService: Committing SQL transaction for provider grouping data backfilling job on specification '{specificationId}'", specificationId);
                    _repo.Commit();
                }
                catch (Exception ex)
                {
                    _logger.Information("Starting rollback for provider grouping data backfilling job for specification '{specificationId}'", specificationId);
                    _repo.RollBack();
                    _logger.Information("SQL rollback complete for provider grouping data backfilling job for specification '{specificationId}'", specificationId);
                    _logger.Error(ex, "Error provider grouping data backfilling job for specification '{specificationId}'", specificationId);
                    throw;
                }
                _logger.Information("ProviderGroupingDataBackfillingService: Execution completed successfully for creating new funding group data and blob");
            }
           
            #endregion
        }

        #region Private methods
        
        private async Task CreateNewFundingGroupDataAndBlob(SpecificationSummary specification,
            FundingConfiguration fundingConfiguration,
            string channelCode,
            IEnumerable<Channel> channels,
            Dictionary<string, PublishedProvider> providers,
            string jobId,
            string correlationId,
            Reference author)
        {
            Dictionary<string, PublishedProviderVersion> providersToBeCreated = providers.Select(_ => _.Value.Released).ToDictionary(_ => _.ProviderId);

            Channel channel = channels.Where(_ => _.ChannelCode.Equals(channelCode)).FirstOrDefault();

            _logger.Information("ProviderGroupingDataBackfillingService: Producing organisation groups for channel '{ChannelCode}'", channel.ChannelCode);
            IEnumerable<OrganisationGroupResult> allOrganisationGroups =
                await _channelOrganisationGroupGeneratorService.GenerateOrganisationGroups(channel,
                    fundingConfiguration,
                    specification,
                    providersToBeCreated.Values);
            _logger.Information("ProviderGroupingDataBackfillingService: A total of {Count} organisation groups are generated for channel '{ChannelCode}'", allOrganisationGroups.Count(), channel.ChannelCode);

            _publishProvidersLoadContext.SetSpecDetails(specification.FundingStreams.First().Id, specification.FundingPeriod.Id);

            _logger.Information("ProviderGroupingDataBackfillingService: Determing which organisation groups to persist with new data for channel '{ChannelCode}'", channel.ChannelCode);
            (IEnumerable<OrganisationGroupResult> organisationGroupsToCreate, Dictionary<string, PublishedProviderVersion> providersInGroupsToCreate) =
                await _channelOrganisationGroupChangeDetector.DetermineFundingGroupsToCreateWhenBackfillingdata(allOrganisationGroups,
                    providersToBeCreated,
                    specification,
                    channel);
            _logger.Information("ProviderGroupingDataBackfillingService: A total of '{Count}' new FundingGroups should be created for channel '{ChannelCode}'", organisationGroupsToCreate.Count(), channel.ChannelCode);

            _logger.Information("ProviderGroupingDataBackfillingService: '{Count}' number of providers required the creation of new funding group data and blob", providersInGroupsToCreate.Count());

            if (organisationGroupsToCreate.Any())
            {
                _logger.Information("ProviderGroupingDataBackfillingService: Creating funding groups for channel '{ChannelCode}'", channel.ChannelCode);
                IEnumerable<FundingGroup> fundingGroups =
                    await _fundingGroupService.CreateFundingGroups(specification.Id, channel.ChannelId, organisationGroupsToCreate);
                _logger.Information("ProviderGroupingDataBackfillingService: Created a total of '{Count}' new funding groups in channel '{ChannelCode}'", fundingGroups.Count(), channel.ChannelCode);

                _logger.Information("ProviderGroupingDataBackfillingService: Generating funding group data (versions) for channel '{ChannelCode}'", channel.ChannelCode);
                IEnumerable<GeneratedPublishedFunding> fundingGroupData =
                    await _fundingGroupDataGenerator.Generate(organisationGroupsToCreate,
                                                              specification,
                                                              channel,
                                                              providersInGroupsToCreate.Keys,
                                                              author,
                                                              jobId,
                                                              correlationId);

                _logger.Information("ProviderGroupingDataBackfillingService: Persisting a total of '{Count}' funding group versions for channel '{ChannelCode}'", fundingGroupData.Count(), channel.ChannelCode);
                IEnumerable<FundingGroupVersion> fundingGroupVersionsCreated = await _fundingGroupDataPersistenceService.ReleaseFundingGroupData(fundingGroupData, channel.ChannelId);

                _logger.Information("ProviderGroupingDataBackfillingService: Updating the FundingGroupProviders table with the fundingversionId");
                await InsertProvidersIntoFundingGroup(organisationGroupsToCreate, fundingGroupVersionsCreated, fundingGroups, specification.Id);

                #region SavePublishedFundingContents

                _logger.Information("ProviderGroupingDataBackfillingService: Retrieving funding group channel versions for specification '{Id}'", specification.Id);
                IEnumerable<LatestProviderVersionInFundingGroup> fundingGroupVersions = await _repo.GetLatestProviderVersionChannelVersionInFundingGroups(specification.Id);
                _logger.Information("ProviderGroupingDataBackfillingService: Building funding group dictionary for specification '{Id}'", specification.Id);
                Dictionary<string, LatestProviderVersionInFundingGroup> fundingGroupVersionsDict =
                    fundingGroupVersions?.ToDictionary(_ => $"{_.ChannelId}-{_.ProviderId}-{_.GroupingReasonCode}-{_.OrganisationGroupTypeCode}-{_.OrganisationGroupIdentifierValue}", _ => _)
                                                                                                        ?? new Dictionary<string, LatestProviderVersionInFundingGroup>();
                IEnumerable<PublishedFundingVersion> publishedFundingVersions = fundingGroupData.Select(_ => _.PublishedFundingVersion);

                _logger.Information("ProviderGroupingDataBackfillingService: Adding funding group channel version for channel '{ChannelCode}'", channel.ChannelCode);
                fundingGroupData.ForEach(fgd =>
                {
                    fgd.OrganisationGroupResult.Providers.ForEach(p =>
                    {
                        List<ChannelVersion> channelVersions = new List<ChannelVersion>();
                        channels.ForEach(c =>
                        {
                            LatestProviderVersionInFundingGroup fundingGroupVersion = null;
                            fundingGroupVersionsDict.TryGetValue(
                                $"{c.ChannelId}-{p.ProviderId}-{fgd.PublishedFundingVersion.GroupingReason}-{fgd.PublishedFundingVersion.OrganisationGroupTypeCode}-{fgd.PublishedFundingVersion.OrganisationGroupIdentifierValue}",
                                out fundingGroupVersion);

                            int fundingGroupChannelVersion = fundingGroupVersion != null ? fundingGroupVersion.ChannelVersion : 0;
                            channelVersions.Add(new ChannelVersion
                            {
                                type = c.ChannelName,
                                value = fundingGroupChannelVersion,
                            });
                        });
                        fgd.PublishedFundingVersion.ChannelVersions = channelVersions;
                    });
                });

                _logger.Information("ProviderGroupingDataBackfillingService: Persisting funding group blob document contents for channel '{ChannelCode}'", channel.ChannelCode);
                await _publishedFundingContentsChannelPersistenceService
                    .SavePublishedFundingContents(publishedFundingVersions, channel);

                _logger.Information("ProviderGroupingDataBackfillingService: Completed release for channel '{ChannelCode}'", channel.ChannelCode);
                #endregion
            }

        }

        private async Task InsertProvidersIntoFundingGroup(IEnumerable<OrganisationGroupResult> organisationGroupsToCreate,
            IEnumerable<FundingGroupVersion> fundingGroupVersionsCreated,
            IEnumerable<FundingGroup> fundingGroupsCreated,
            string specificationId)
        {
            IEnumerable<SqlModels.GroupingReason> groupingReasons = await _repo.GetGroupingReasons();
            Dictionary<string, int> groupingReasonIdLookupByCode = groupingReasons.ToDictionary(_ => _.GroupingReasonCode, _ => _.GroupingReasonId);

            List<FundingGroupProvider> createFundingGroupProviders = new List<FundingGroupProvider>();

            foreach (OrganisationGroupResult organisationGroup in organisationGroupsToCreate)
            {
                foreach (Common.ApiClient.Providers.Models.Provider provider in organisationGroup.Providers)
                {
                    Guid fundingGroupId = fundingGroupsCreated.Where(_ => _.GroupingReasonId == groupingReasonIdLookupByCode[organisationGroup.GroupReason.ToString()]
                   && _.OrganisationGroupTypeCode.Equals(organisationGroup.GroupTypeCode.ToString())
                   && _.OrganisationGroupIdentifierValue.Equals(organisationGroup.IdentifierValue)).Select(_ => _.FundingGroupId).FirstOrDefault();

                    FundingGroupVersion fundingGroupVersion = fundingGroupVersionsCreated.Where(_ => _.FundingGroupId == fundingGroupId).FirstOrDefault();

                    IEnumerable<ReleasedProviderVersionChannel> releasedProviderVersionChannel = await _repo.GetReleasedProviderVersionChannelUsingAmbientTransation(specificationId, provider.ProviderId, fundingGroupVersion.ChannelId );

                    FundingGroupProvider fundingGroupProvider = new FundingGroupProvider()
                    {
                        FundingGroupProviderId = _fundingGroupIdentifierGenerator.GenerateIdentifier(),
                        FundingGroupVersionId = fundingGroupVersion.FundingGroupVersionId,
                        ReleasedProviderVersionChannelId = releasedProviderVersionChannel.FirstOrDefault().ReleasedProviderVersionChannelId,
                    };

                    createFundingGroupProviders.Add(fundingGroupProvider);
                }
            }

            _logger.Information("ProviderGroupingDataBackfillingService: Persisting a total of '{Count}' funding group providers", createFundingGroupProviders.Count);
            await _repo.BulkCreateFundingGroupProvidersUsingAmbientTransaction(createFundingGroupProviders);
        }

        #endregion
    }

}
