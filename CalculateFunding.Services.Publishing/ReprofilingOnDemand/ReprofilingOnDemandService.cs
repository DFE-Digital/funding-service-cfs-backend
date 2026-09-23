using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.TemplateMetadata.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Generators.OrganisationGroup.Models;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Processing;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using Polly;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.ReprofilingOnDemand
{
    public class ReprofilingOnDemandService : JobProcessingService, IReprofilingOnDemandService
    {
        private const string SfaCorrelationId = "sfa-correlationId";
        private readonly IPublishedFundingDataService _publishedFundingDataService;
        private readonly ISpecificationService _specificationService;
        private readonly IProviderService _providerService;
        private readonly IPublishedProviderDataPopulator _publishedProviderDataPopulator;
        private readonly ILogger _logger;
        private readonly IPrerequisiteCheckerLocator _prerequisiteCheckerLocator;
        private readonly AsyncPolicy _publishingResiliencePolicy;
        private readonly IPublishProviderExclusionCheck _providerExclusionCheck;
        private readonly IFundingLineValueOverride _fundingLineValueOverride;
        private readonly IInformationLinesAggregationService _informationLinesAggregationService;
        private readonly IJobManagement _jobManagement;
        private readonly ITransactionFactory _transactionFactory;
        private readonly IPublishedProviderVersionService _publishedProviderVersionService;
        private readonly IPoliciesService _policiesService;
        private readonly IVariationService _variationService;
        private readonly IReApplyCustomProfiles _reApplyCustomProfiles;
        private readonly IRefreshStateService _refreshStateService;
        private readonly IOrganisationGroupService _organisationGroupService;
        private readonly IChannelOrganisationGroupGeneratorService _channelOrganisationGroupGeneratorService;
        public ReprofilingOnDemandService(IPublishedFundingDataService publishedFundingDataService,
                    IPublishingResiliencePolicies publishingResiliencePolicies,
                    ISpecificationService specificationService,
                    IProviderService providerService,
                    ICalculationResultsService calculationResultsService,
                    IPublishedProviderDataPopulator publishedProviderDataPopulator,
                    ILogger logger,
                    IPrerequisiteCheckerLocator prerequisiteCheckerLocator,
                    IPublishProviderExclusionCheck providerExclusionCheck,
                    IFundingLineValueOverride fundingLineValueOverride,
                    IInformationLinesAggregationService informationLinesAggregationService,
                    IJobManagement jobManagement,
                    IVariationService variationService,
                    ITransactionFactory transactionFactory,
                    IPublishedProviderVersionService publishedProviderVersionService,
                    IPoliciesService policiesService,
                    IReApplyCustomProfiles reApplyCustomProfiles,
                    IRefreshStateService refreshStateService,
                    IOrganisationGroupService organisationGroupService,
                    IChannelOrganisationGroupGeneratorService channelOrganisationGroupGeneratorService) : base(jobManagement, logger)
        {            
            Guard.ArgumentNotNull(publishedFundingDataService, nameof(publishedFundingDataService));
            Guard.ArgumentNotNull(publishingResiliencePolicies, nameof(publishingResiliencePolicies));
            Guard.ArgumentNotNull(specificationService, nameof(specificationService));
            Guard.ArgumentNotNull(providerService, nameof(providerService));
            Guard.ArgumentNotNull(calculationResultsService, nameof(calculationResultsService));
            Guard.ArgumentNotNull(publishedProviderDataPopulator, nameof(publishedProviderDataPopulator));
            Guard.ArgumentNotNull(providerExclusionCheck, nameof(providerExclusionCheck));
            Guard.ArgumentNotNull(fundingLineValueOverride, nameof(fundingLineValueOverride));
            Guard.ArgumentNotNull(informationLinesAggregationService, nameof(informationLinesAggregationService));
            Guard.ArgumentNotNull(jobManagement, nameof(jobManagement));
            Guard.ArgumentNotNull(variationService, nameof(variationService));
            Guard.ArgumentNotNull(transactionFactory, nameof(transactionFactory));
            Guard.ArgumentNotNull(publishedProviderVersionService, nameof(publishedProviderVersionService));
            Guard.ArgumentNotNull(policiesService, nameof(policiesService));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(prerequisiteCheckerLocator, nameof(prerequisiteCheckerLocator));
            Guard.ArgumentNotNull(reApplyCustomProfiles, nameof(reApplyCustomProfiles));
            Guard.ArgumentNotNull(publishingResiliencePolicies.PublishedFundingRepository, nameof(publishingResiliencePolicies.PublishedFundingRepository));
            Guard.ArgumentNotNull(refreshStateService, nameof(refreshStateService));
            Guard.ArgumentNotNull(organisationGroupService, nameof(organisationGroupService));
            Guard.ArgumentNotNull(channelOrganisationGroupGeneratorService, nameof(channelOrganisationGroupGeneratorService));

            _publishedFundingDataService = publishedFundingDataService;
            _specificationService = specificationService;
            _providerService = providerService;
            _publishedProviderDataPopulator = publishedProviderDataPopulator;
            _logger = logger;
            _prerequisiteCheckerLocator = prerequisiteCheckerLocator;
            _providerExclusionCheck = providerExclusionCheck;
            _fundingLineValueOverride = fundingLineValueOverride;
            _informationLinesAggregationService = informationLinesAggregationService;
            _variationService = variationService;
            _reApplyCustomProfiles = reApplyCustomProfiles;

            _publishingResiliencePolicy = publishingResiliencePolicies.PublishedFundingRepository;
            _jobManagement = jobManagement;
            _transactionFactory = transactionFactory;
            _publishedProviderVersionService = publishedProviderVersionService;
            _policiesService = policiesService;
            _refreshStateService = refreshStateService;
            _organisationGroupService = organisationGroupService;
            _channelOrganisationGroupGeneratorService = channelOrganisationGroupGeneratorService;
        }

        public override async Task Process(ServiceBusReceivedMessage message)
        {
            Guard.ArgumentNotNull(message, nameof(message));

            Reference author = message.GetUserDetails();

            string specificationId = message.ApplicationProperties["specification-id"] as string;

            SpecificationSummary specification = await _specificationService.GetSpecificationSummaryById(specificationId);

            if (specification == null)
            {
                throw new NonRetriableException($"Reprofilingondemand :Could not find specification with id for '{specificationId}'");
            }

            PublishedProviderIdsRequest publishedProviderIdsRequest = message.GetPayloadAsInstanceOf<PublishedProviderIdsRequest>();
            string[] providerIds = publishedProviderIdsRequest.PublishedProviderIds.Select(_=>_).ToArray();

            // Get scoped providers for this specification
            IDictionary<string, Provider> scopedProvidersForSpec = await _providerService.GetScopedProvidersForSpecification(specification.Id, specification.ProviderVersionId);

            //Intersection of selected providers in scoped providers for spec
            IDictionary<string, Provider> scopedProviders = scopedProvidersForSpec.Where(_ => providerIds.Contains(_.Key)).ToDictionary(_ => _.Key, _ => _.Value);

            if (!scopedProviders.IsNullOrEmpty())
            {
                _logger.Information($"Reprofilingondemand :Found {scopedProviders.Count} scoped providers");
            }
            else
            {
                string messageText = "No scoped providers found for Reprofiling";
                _logger.Information(messageText);
                await _jobManagement.UpdateJobStatus(Job.Id, 0, 0, false, "No scoped providers found for Reprofiling.");
                throw new NonRetriableException(messageText);
            }

            // Get existing published providers for this specification
            _logger.Information("Reprofilingondemand :Looking up existing published providers from cosmos");

            IDictionary<string, List<PublishedProvider>> existingPublishedProvidersByFundingStream = new Dictionary<string, List<PublishedProvider>>();
            foreach (Reference fundingStream in specification.FundingStreams)
            {
                //Todo- We may need a new api to get the selected provider details rather than all provider details
                List<PublishedProvider> publishedProviders = (await _publishingResiliencePolicy.ExecuteAsync(() =>
                _publishedFundingDataService.GetCurrentPublishedProviders(fundingStream.Id, specification.FundingPeriod.Id, providerIds))).ToList();

                existingPublishedProvidersByFundingStream.Add(fundingStream.Id, publishedProviders);

                _logger.Information($"Reprofilingondemand :Found {publishedProviders.Count} existing published providers for funding stream {fundingStream.Id} from cosmos");
            }

            _logger.Information("Verifying prerequisites for Reprofilingondemand");

            // Check prerequisites for this specification to be chosen/reProfileOnDemand
            IPrerequisiteChecker prerequisiteChecker = _prerequisiteCheckerLocator.GetPreReqChecker(PrerequisiteCheckerType.ReProfileOnDemand);
            try
            {
                await prerequisiteChecker.PerformChecks(specification, Job.Id, existingPublishedProvidersByFundingStream.SelectMany(x => x.Value), scopedProviders?.Values);
            }
            catch (JobPrereqFailedException ex)
            {
                await _jobManagement.UpdateJobStatus(Job.Id, 0, 0, false, "ReprofilingOnDemand: Prerequisite check failed: {ex.Message}");
                throw new NonRetriableException(ex.Message, ex);
            }
            _logger.Information("Prerequisites for reprofilingondemand passed");          

            string correlationId = message.GetUserProperty<string>(SfaCorrelationId);
           
           try
            {
                foreach (Reference fundingStream in specification.FundingStreams)
                {
                    _logger.Information($"Starting to reprofilingondemand funding for '{fundingStream.Id}'");

                    await ReProfileOnDemand(fundingStream,
                        specification,
                        scopedProviders,
                        Job.Id,
                        author,
                        correlationId,
                        existingPublishedProvidersByFundingStream[fundingStream.Id],
                        specification.FundingPeriod);

                    _logger.Information($"Finished processing reprofilingondemand funding for '{fundingStream.Id}'");

                }
            }
            finally
            {                
                string jobStatusMessage = providerIds.Length != _refreshStateService.UpdatedProviders.Count() 
                    ? $"{_refreshStateService.UpdatedProviders.Count()} eligible providers have been reprofiled successfully."
                    : $"All {providerIds.Length} eligible providers in this batch have been reprofiled successfully.";

                _logger.Information("Starting to clear reprofilingondemand variation snapshots");
                _variationService.ClearSnapshots();
                _logger.Information("Finished clearing reprofilingondemand variation snapshots");
                await _jobManagement.UpdateJobStatus(Job.Id, 0, 0, true, jobStatusMessage);
            }
        }

        private async Task ReProfileOnDemand(Reference fundingStream,
                   SpecificationSummary specification,
                   IDictionary<string, Provider> scopedProviders,
                   string jobId, Reference author,
                   string correlationId,
                   IEnumerable<PublishedProvider> existingPublishedProviders,
                   Reference fundingPeriod)
        {
            TemplateMetadataContents templateMetadataContents = await _policiesService.GetTemplateMetadataContents(fundingStream.Id, specification.FundingPeriod.Id, specification.TemplateIds[fundingStream.Id]);

            if (templateMetadataContents == null)
            {
                _logger.Information($"Reprofilingondemand :Unable to locate template meta data contents for funding stream:'{fundingStream.Id}' and template id:'{specification.TemplateIds[fundingStream.Id]}'");
                return;
            }

            IEnumerable<ProfileVariationPointer> variationPointers = await _specificationService.GetProfileVariationPointers(specification.Id) ?? ArraySegment<ProfileVariationPointer>.Empty;

            Dictionary<string, PublishedProvider> publishedProviders = new Dictionary<string, PublishedProvider>();
            IDictionary<string, GeneratedProviderResult> generatedPublishedProviderData = new Dictionary<string, GeneratedProviderResult>();

            _logger.Information("Looking up existing publishedproviders calculations for reprofilingondemand");
            foreach (PublishedProvider publishedProvider in existingPublishedProviders)
            {
                if (publishedProvider.Current.FundingStreamId == fundingStream.Id)
                {
                    publishedProviders.Add(publishedProvider.Current.ProviderId, publishedProvider);

                    GeneratedProviderResult generatedProviderResult = new GeneratedProviderResult()
                    {
                        Calculations = publishedProvider.Current.Calculations,
                        FundingLines = publishedProvider.Current?.FundingLines,
                        Provider = publishedProvider.Current?.Provider,
                        ReferenceData = publishedProvider.Current?.ReferenceData,
                        TotalFunding = publishedProvider.Current?.TotalFunding,
                    };
                    generatedPublishedProviderData.Add(publishedProvider.Current.ProviderId, generatedProviderResult);
                }
            }
            _logger.Information($"Reprofilingondemand :Found existing publishedproviders calculations for {generatedPublishedProviderData?.Count} providers from cosmos");

            if (!generatedPublishedProviderData.Any())
            {
                throw new Exception($"Reprofilingondemand : existing publishedproviders calculations returned null for specification {specification.Id}");
            }

            // store the readonly dictionary of published providers against the refresh state service so we only add 
            // providers to be updated if there are differences to persist
            _refreshStateService.ExistingCurrentPublishedProviders = publishedProviders.DeepCopy().ToDictionary(_ => _.Key, _ => _.Value.Current);

            Common.TemplateMetadata.Models.FundingLine[] flattenedTemplateFundingLines = templateMetadataContents.RootFundingLines.Flatten(_ => _.FundingLines).ToArray();

            _logger.Information("Reprofilingondemand :Start snapshots for published provider variations");
            // snapshot the current published providers so any changes aren't reflected when we detect variations later
            _variationService.SnapShot(publishedProviders, fundingStream.Id);
            _logger.Information("Reprofilingondemand :Finished snapshots for published provider variations");

            //we need enumerate a readonly cut of this as we add to it in some variations now (for missing providers not in scope)
            Dictionary<string, PublishedProvider> publishedProvidersReadonlyDictionary = publishedProviders.ToDictionary(_ => _.Key, _ => _.Value);

            _logger.Information($"Reprofilingondemand :Start getting funding configuration for funding stream '{fundingStream.Id}'");
            // set up the published providers context for error detection laterawait 
            FundingConfiguration fundingConfiguration = await _policiesService.GetFundingConfiguration(fundingStream.Id, specification.FundingPeriod.Id);
            _logger.Information($"Reprofilingondemand :Retrieved funding stream configuration for '{fundingStream.Id}'");

            HashSet<string> indicativeStatus = new HashSet<string>(fundingConfiguration?.IndicativeOpenerProviderStatus ?? ArraySegment<string>.Empty);

            Dictionary<string, IEnumerable<OrganisationGroupResult>> organisationGroupResultsData =
                await _organisationGroupService.GenerateOrganisationGroups(
                    scopedProviders.Values,
                    publishedProvidersReadonlyDictionary.Values,
                    fundingConfiguration,
                    specification.ProviderVersionId,
                    specification.ProviderSnapshotId);

            IDictionary<string, IEnumerable<OrganisationGroupResult>> channelOrganisationGroupResultsData =
                await _channelOrganisationGroupGeneratorService.GenerateOrganisationGroupsForAllChannels(
                    fundingConfiguration,
                    specification,
                    publishedProvidersReadonlyDictionary.Values.Select(_ => _.Current));

            PublishedProvidersContext publishedProvidersContext = new PublishedProvidersContext
            {
                ScopedProviders = scopedProviders.Values,
                SpecificationId = specification.Id,
                ProviderVersionId = specification.ProviderVersionId,
                CurrentPublishedFunding = (await _publishingResiliencePolicy.ExecuteAsync(() => _publishedFundingDataService.GetCurrentPublishedFunding(specification.Id, GroupingReason.Payment)))
                    .Where(x => x.Current.GroupingReason == CalculateFunding.Models.Publishing.GroupingReason.Payment),
                OrganisationGroupResultsData = organisationGroupResultsData,
                ChannelOrganisationGroupResultsData = channelOrganisationGroupResultsData,
                FundingConfiguration = fundingConfiguration,
                VariationContexts = new ConcurrentDictionary<string, ProviderVariationContext>()
            };

            _logger.Information("Reprofilingondemand :Starting to process providers for variations and exclusions");

            foreach (KeyValuePair<string, PublishedProvider> publishedProvider in publishedProvidersReadonlyDictionary)
            {
                PublishedProviderVersion publishedProviderVersion = publishedProvider.Value.Current;

                PublishedProviderVersion preRefreshProviderVersion = publishedProvider.Value.Current.DeepCopy();

                // need to reset the variation reasons so we don't carry over variation reasons on a refresh
                publishedProviderVersion.VariationReasons = Array.Empty<VariationReason>();

                string providerId = publishedProviderVersion.ProviderId;

                bool providerExists = scopedProviders.ContainsKey(providerId);

                // Handle the case where a provider has a record in funding approvals
                // but when refresh funding is run, it's now no longer in the specification's scoped provider list
                if (!providerExists)
                {
                    // When there is no released funding for this provider
                    if (publishedProvider.Value.Released == null)
                    {
                        _refreshStateService.Delete(publishedProvider.Value);
                        continue;
                    }
                    else
                    {
                        _refreshStateService.Update(publishedProvider.Value);
                    }
                }

                generatedPublishedProviderData.TryGetValue(publishedProvider.Key, out GeneratedProviderResult generatedProviderResult);

                bool publishedProviderUpdated = false;
                IEnumerable<string> variances = ArraySegment<string>.Empty;

                if (providerExists)
                {
                    // need to bypass exclusion check if there are no calculations as this is written for when calcs are written to exclude certain providers
                    // this can be the case for a successor that has been created through a refresh run 
                    if (generatedProviderResult.HasCalculations)
                    {
                        PublishedProviderExclusionCheckResult exclusionCheckResult = _providerExclusionCheck.ShouldBeExcluded(publishedProvider.Key,
                                generatedProviderResult,
                                flattenedTemplateFundingLines);

                        if (exclusionCheckResult.ShouldBeExcluded)
                        {
                            if (_refreshStateService.Exclude(publishedProvider.Value))
                            {
                                continue;
                            }

                            // if there is no previous funding for the generated funding lines then remove
                            if (publishedProvider.Value.Released == null || !_fundingLineValueOverride.HasPreviousFunding(generatedProviderResult, publishedProviderVersion))
                            {
                                _refreshStateService.Delete(publishedProvider.Value);
                                continue;
                            }
                        }

                        _fundingLineValueOverride.OverridePreviousFundingLineValues(publishedProvider.Value, generatedProviderResult);
                    }

                    // apply custom profiles to generated result
                    _reApplyCustomProfiles.ProcessPublishedProvider(publishedProviderVersion, generatedProviderResult);

                    (publishedProviderUpdated, variances) = _publishedProviderDataPopulator.UpdatePublishedProvider(publishedProviderVersion,
                        generatedProviderResult,
                        scopedProviders[providerId],
                        specification.TemplateIds[fundingStream.Id],
                        _refreshStateService.IsNewProvider(publishedProvider.Value),
                        publishedProviderVersion.ReProfileAudits);

                    // need to set indicative flag here as we only copy the provider in the above code unless it's a new provider
                    if (publishedProviderVersion.SetIsIndicative(indicativeStatus))
                    {
                        publishedProviderUpdated = true;
                        variances = variances.Concat(new[] { "Indicative flag set" });
                    }

                    _logger.Information($"Reprofilingondemand :Published provider '{publishedProvider.Key}' updated: '{publishedProviderUpdated}'");
                }

                if (existingPublishedProviders.AnyWithNullCheck() && scopedProviders.ContainsKey(providerId))
                {
                    ProviderVariationContext context = await _variationService.PrepareVariedProviders(generatedProviderResult.TotalFunding ?? 0,
                        publishedProviders,
                        publishedProvider.Value,
                        scopedProviders[providerId],
                        fundingConfiguration.ReprofilingOnDemandVariations,
                        variationPointers,
                        fundingStream.Id,
                        specification.ProviderVersionId,
                        organisationGroupResultsData,
                        variances,
                        fundingStream.Id,
                        fundingPeriod.Id,
                        preRefreshProviderVersion, fundingConfiguration, true);

                    publishedProvidersContext.VariationContexts.Add(providerId, context);

                    if (context != null && context.Successor != null)
                    {
                        // if the successor already exists then we still need to add it to existing providers to update
                        _refreshStateService.Update(context.Successor);
                    }
                }

                if (publishedProviderUpdated)
                {
                    _refreshStateService.Update(publishedProvider.Value);
                }
                
                if (!publishedProviderUpdated)
                {
                    continue;
                }
            }

            _logger.Information("ReprofilingOnDemand: Finished processing providers for variations and exclusions");

            _logger.Information("Reprofilingondemand :Starting to apply variations");

            if (!await _variationService.ApplyVariations(_refreshStateService, specification.Id, jobId))
            {
                await _jobManagement.UpdateJobStatus(jobId, 0, 0, false, "Reprofilingondemand job failed with variations errors.");

                throw new NonRetriableException($"Reprofilingondemand :Unable to reprofilingondemand funding. Variations generated {_variationService.ErrorCount} errors. Check log for details");
            }

            if (fundingConfiguration != null && fundingConfiguration.EnableInformationLineAggregation)
            {
                foreach (PublishedProvider publishedProvider in _refreshStateService.AllProviders)
                {
                    _informationLinesAggregationService.AggregateFundingLines(specification.Id,
                        publishedProvider.Current.ProviderId,
                        publishedProvider.Current.FundingLines,
                        templateMetadataContents.RootFundingLines);
                }
            }

            _logger.Information("Reprofilingondemand :Finished applying variations");

            _logger.Information($"Reprofilingondemand :Adding or updating a total of {_refreshStateService.Count} published providers");

            if (_refreshStateService.Count > 0)
            {
                using (Transaction transaction = _transactionFactory.NewTransaction<ReprofilingOnDemandService>())
                {
                    try
                    {
                        // if any error occurs while updating or indexing then we need to re-index all published providers for consistency
                        transaction.Enroll(async () =>
                        {
                            await _publishedProviderVersionService.CreateReIndexJob(author, correlationId, specification.Id, jobId);
                        });

                        await _refreshStateService.Persist(jobId, author, correlationId);

                        transaction.Complete();
                    }
                    catch (Exception)
                    {
                        await transaction.Compensate();

                        throw;
                    }
                }

            }
        }
    }
}
