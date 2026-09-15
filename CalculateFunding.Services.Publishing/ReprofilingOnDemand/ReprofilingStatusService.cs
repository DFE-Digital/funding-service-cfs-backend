using CalculateFunding.Common.Storage;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Publishing.Interfaces;
using Serilog;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Services.Publishing.Models;
using Microsoft.AspNetCore.Mvc;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Services.Core.Extensions;
using Newtonsoft.Json;
using CalculateFunding.Models.Publishing.Reprofiling;
using System.IO;
using Microsoft.Azure.Storage.Blob;
using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.Models;
using ApiJob = CalculateFunding.Common.ApiClient.Jobs.Models.Job;
using CalculateFunding.Services.Core.Constants;

namespace CalculateFunding.Services.Publishing.ReprofilingOnDemand
{
    public class ReprofilingStatusService : IReprofilingStatusService
    {
        private const string BlobContainerName = "publishingconfirmation";

        private readonly IPublishedProviderReprofilingSummaryProcessor _publishedProviderReprofilingSummaryProcessor;
        private readonly ISpecificationService _specificationService;
        private readonly AsyncPolicy _blobClientPolicy;
        private readonly ICsvUtils _csvUtils;
        private readonly IBlobClient _blobClient;
        private readonly IPoliciesService _policiesService;
        private readonly ILogger _logger;
        private readonly ICreateJobsForReprofilingOnDemand _reprofilingOnDemandJobCreationService;

        public ReprofilingStatusService(
            ISpecificationService specificationService,
            ICreateJobsForReprofilingOnDemand reprofilingOnDemandJobCreationService,
            IPublishedFundingRepository publishedFundingRepository,
            IPublishingResiliencePolicies publishingResiliencePolicies,
            ICsvUtils csvUtils,
            IBlobClient blobClient,
            IPublishedProviderReprofilingSummaryProcessor publishedProviderReprofilingSummaryProcessor,
            IPoliciesService policiesService,
            ILogger logger)
        {
            Guard.ArgumentNotNull(specificationService, nameof(specificationService));
            Guard.ArgumentNotNull(reprofilingOnDemandJobCreationService, nameof(reprofilingOnDemandJobCreationService));
            Guard.ArgumentNotNull(publishingResiliencePolicies, nameof(publishingResiliencePolicies));
            Guard.ArgumentNotNull(publishingResiliencePolicies.BlobClient, nameof(publishingResiliencePolicies.BlobClient));
            Guard.ArgumentNotNull(csvUtils, nameof(csvUtils));
            Guard.ArgumentNotNull(blobClient, nameof(blobClient));
            Guard.ArgumentNotNull(publishedProviderReprofilingSummaryProcessor, nameof(publishedProviderReprofilingSummaryProcessor));
            Guard.ArgumentNotNull(policiesService, nameof(policiesService));
            Guard.ArgumentNotNull(logger, nameof(logger));

            _specificationService = specificationService;
            _reprofilingOnDemandJobCreationService = reprofilingOnDemandJobCreationService;
            _blobClientPolicy = publishingResiliencePolicies.BlobClient;
            _csvUtils = csvUtils;
            _blobClient = blobClient;
            _publishedProviderReprofilingSummaryProcessor = publishedProviderReprofilingSummaryProcessor;
            _policiesService = policiesService;
            _logger = logger;
        }

        public async Task<IActionResult> GetProviderBatchResultForReprofiling(PublishedProviderIdsRequest providerIds,
            string specificationId)
            => await GetProviderBatchResultForStatuses(providerIds, specificationId);

        public async Task<IActionResult> QueueReprofilingOnDemand(string specificationId, PublishedProviderIdsRequest publishedProviderIdsRequest, Reference user, string correlationId)
        {
            ApiJob job = await _reprofilingOnDemandJobCreationService.CreateJob(specificationId, user, correlationId, messageBody: JsonExtensions.AsJson(publishedProviderIdsRequest), compress: true);
            return ProcessJobResponse(job, specificationId, JobConstants.DefinitionNames.ReprofilingOnDemandJob);
        }

        private IActionResult ProcessJobResponse(ApiJob job, string specificationId, string jobType)
        {
            if (job != null)
            {
                JobCreationResponse jobCreationResponse = new JobCreationResponse()
                {
                    JobId = job.Id,
                };

                return new OkObjectResult(jobCreationResponse);
            }
            else
            {
                string errorMessage = $"Failed to create job of type '{jobType}' on specification '{specificationId}'";

                return new InternalServerErrorResult(errorMessage);
            }
        }

        private async Task<IActionResult> GetProviderBatchResultForStatuses(PublishedProviderIdsRequest providerIds,
            string specificationId)
        {
            SpecificationSummary specificationSummary = await _specificationService.GetSpecificationSummaryById(specificationId);

            IEnumerable<ProfileVariationPointer> currentProfileVariationPointers = await _specificationService.GetProfileVariationPointers(specificationId);

            FundingConfiguration fundingConfiguration = await _policiesService.GetFundingConfiguration(
                specificationSummary.FundingStreams.First().Id, specificationSummary.FundingPeriod.Id);

            FundingPeriod fundingPeriod = await _policiesService.GetFundingPeriodByConfigurationId(fundingConfiguration?.FundingPeriodId);

            ReprofilingSummaryResult reprofilingSummary;
            try
            {
                reprofilingSummary =
                    await _publishedProviderReprofilingSummaryProcessor.GetFundingSummaryForReprofilingPublishedProviders(
                        providerIds.PublishedProviderIds,
                        specificationSummary,
                        fundingConfiguration,
                        fundingPeriod,
                        currentProfileVariationPointers);

                if(reprofilingSummary != null)
                {
                    reprofilingSummary.Url = await GetProviderReprofilingDataAsCsv(specificationId, reprofilingSummary, fundingConfiguration);
                }

                return new OkObjectResult(reprofilingSummary);
            }
            catch (KeyNotFoundException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error in GetProviderBatchResultForReprofiling for specification {specificationId} with params {JsonConvert.SerializeObject(providerIds)}: {ex.Message}");
                return new InternalServerErrorResult("An error occurred while generating funding summary");
            }
        }


        private async Task<string> GetProviderReprofilingDataAsCsv(string specificationId, ReprofilingSummaryResult reprofilingSummary, FundingConfiguration fundingConfiguration)
        {
            string csvFileSuffix = $"ProvidersToReprofile-{DateTime.UtcNow:yyyyMMdd-HHmmssffff}";
            string blobUrl = string.Empty;

            if (reprofilingSummary == null || reprofilingSummary.TotalProviders == 0)
            {
                return blobUrl;
            }

            IEnumerable<dynamic> csvRows = reprofilingSummary.ProviderSummaryResult.Select(x => new
            {
                UKPRN = x.UKPRN,
                Name = x.Name,
                OpenDate = x.OpenDate?.ToString("dd/MM/yyyy"),
                IsEligible = x.IsEligible
            });

            string csvFileData = _csvUtils.AsCsv(csvRows, true);

            string csvFileName = $"{fundingConfiguration.FundingStreamId}-{fundingConfiguration.FundingPeriodId}-{csvFileSuffix}.csv";

            string blobName = $"{csvFileName}";

            await _blobClientPolicy.ExecuteAsync(async () =>
            {
                ICloudBlob blob = _blobClient.GetBlockBlobReference(blobName, BlobContainerName);
                blob.Properties.ContentDisposition = $"attachment; filename={csvFileName}";

                using (MemoryStream stream = new MemoryStream(csvFileData.AsUTF8Bytes()))
                {
                    await blob.UploadFromStreamAsync(stream);
                }

                blob.Metadata["fundingStreamId"] = fundingConfiguration.FundingStreamId;
                blob.Metadata["fundingPeriodId"] = fundingConfiguration.FundingPeriodId;
                blob.Metadata["specificationId"] = specificationId;
                blob.Metadata["fileName"] = Path.GetFileNameWithoutExtension(csvFileName);
                blob.SetMetadata();

                blobUrl = _blobClient.GetBlobSasUrl(blobName, DateTimeOffset.Now.AddDays(1), SharedAccessBlobPermissions.Read, BlobContainerName);
            });

            return blobUrl;
        }
    }
}
