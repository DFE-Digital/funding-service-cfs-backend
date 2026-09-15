using CalculateFunding.Common.ApiClient.Specifications.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Common.ApiClient.Results;
using CalculateFunding.Common.ApiClient.Results.Models;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.JobManagement;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.CalcEngine.Caching;
using CalculateFunding.Services.CalcEngine.Interfaces;
using CalculateFunding.Services.Core.Constants;
using Newtonsoft.Json;
using Polly;
using Serilog;
using ProviderResult = CalculateFunding.Models.Calcs.ProviderResult;

namespace CalculateFunding.Services.CalcEngine
{
    public class ProviderResultsRepository : IProviderResultsRepository
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger _logger;

        private readonly IProviderResultCalculationsHashProvider _calculationsHashProvider;
        private readonly AsyncPolicy _resultsApiClientPolicy;
        private readonly AsyncPolicy _calculationResultsRepositoryPolicy;
        private readonly IResultsApiClient _resultsApiClient;
        private readonly IJobManagement _jobManagement;

        public ProviderResultsRepository(
            IUnitOfWork uow,
            ILogger logger,
            IProviderResultCalculationsHashProvider calculationsHashProvider,
            ICalculatorResiliencePolicies calculatorResiliencePolicies,
            IResultsApiClient resultsApiClient,
            IJobManagement jobManagement)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(calculationsHashProvider, nameof(calculationsHashProvider));
            Guard.ArgumentNotNull(calculatorResiliencePolicies, nameof(calculatorResiliencePolicies));
            Guard.ArgumentNotNull(resultsApiClient, nameof(resultsApiClient));
            Guard.ArgumentNotNull(calculatorResiliencePolicies.ResultsApiClient, nameof(calculatorResiliencePolicies.ResultsApiClient));
            Guard.ArgumentNotNull(jobManagement, nameof(jobManagement));

            _uow = uow;
            _logger = logger;

            _calculationsHashProvider = calculationsHashProvider;
            _resultsApiClient = resultsApiClient;
            _resultsApiClientPolicy = calculatorResiliencePolicies.ResultsApiClient;
            _calculationResultsRepositoryPolicy = calculatorResiliencePolicies.CalculationResultsRepository;
            _jobManagement = jobManagement;
        }

        public async Task<(long saveToCosmosElapsedMs, long saveToSearchElapsedMs, int savedProviders)> SaveProviderResults(
            IEnumerable<ProviderResult> providerResults,
            SpecificationSummary specificationSummary,
            int partitionIndex,
            int partitionSize,
            Reference user,
            string correlationId,
            string parentJobId)
        {
            if (providerResults == null || providerResults.Count() == 0)
            {
                return (0, 0, 0);
            }

            string batchSpecificationId = providerResults.First().SpecificationId;

            _calculationsHashProvider.StartBatch(batchSpecificationId, partitionIndex, partitionSize);

            //only leave the provider results where the calculation results have changed since they were last saved
            // ToArray required due to reevaulation of the providerResults further down
            providerResults = providerResults.Where(_ => ResultsHaveChanged(_, partitionIndex, partitionSize)).ToArray();

            IEnumerable<KeyValuePair<string, ProviderResult>> results = providerResults.Select(m => new KeyValuePair<string, ProviderResult>(m.Provider.Id, m));

            long sqlSaveTime = await BulkSaveProviderResults(results);

            // Need to wait until the save provider results completed before triggering the search index writer jobs.
            long queueSearchWriterJobTime = await QueueSearchIndexWriterJob(providerResults, specificationSummary, user, correlationId, parentJobId);

            // Only save batch to redis if it has been saved successfully. This enables the message to be requeued for throttled scenarios and will resave to sql/search
            _calculationsHashProvider.EndBatch(batchSpecificationId, partitionIndex, partitionSize);

            await QueueMergeSpecificationJobsInBatches(providerResults, specificationSummary);

            return (sqlSaveTime, queueSearchWriterJobTime, providerResults.Count());
        }

        private async Task<long> QueueSearchIndexWriterJob(
            IEnumerable<ProviderResult> providerResults, 
            SpecificationSummary specificationSummary, 
            Reference user, 
            string correlationId,
            string parentJobId)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string specificationId = specificationSummary.GetSpecificationId();
            IEnumerable<string> providerIds = providerResults.Select(x => x.Provider?.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (!providerIds.Any())
            {
                return 0;
            }

            try
            {
                Common.ApiClient.Jobs.Models.Job searchIndexWriterJob = await _jobManagement.QueueJob(
                    new JobCreateModel()
                    {
                        Trigger = new Trigger
                        {
                            EntityId = specificationId,
                            EntityType = "Specification",
                            Message = "Write ProviderCalculationResultsIndex serach index for specification"
                        },
                        InvokerUserId = user.Id,
                        InvokerUserDisplayName = user.Name,
                        JobDefinitionId = JobConstants.DefinitionNames.SearchIndexWriterJob,
                        ParentJobId = parentJobId,
                        SpecificationId = specificationId,
                        CorrelationId = correlationId,
                        Properties = new Dictionary<string, string>
                        {
                            {"specification-id", specificationId},
                            {"specification-name", specificationSummary.Name},
                            {"index-writer-type", SearchIndexWriterTypes.ProviderCalculationResultsIndexWriter }
                        },
                        MessageBody = JsonConvert.SerializeObject(providerIds)
                    });
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to queue SearchIndexWriterJob for specification - {specificationId}";
                _logger.Error(ex, errorMessage);
                throw;
            }

            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        private bool ResultsHaveChanged(ProviderResult providerResult, int partitionIndex, int partitionSize)
        {
            bool hasChanged = _calculationsHashProvider.TryUpdateCalculationResultHash(providerResult, partitionIndex, partitionSize);

            if (!hasChanged)
            {
                _logger.Verbose(
                    $"Provider:{providerResult.Provider.Id} Spec:{providerResult.SpecificationId} results have no changes so will not be stored this time");
            }

            return hasChanged;
        }

        private async Task<long> BulkSaveProviderResults(IEnumerable<KeyValuePair<string, ProviderResult>> providerResults)
        {
            Stopwatch stopwatchInner = Stopwatch.StartNew();

            foreach (KeyValuePair<string, ProviderResult> kv in providerResults)
            {
                ProviderResult providerResult = kv.Value;

                // Upsert ProviderResultEntity
                var pr = new CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderResult
                {
                    ProviderResultId = providerResult.Id,
                    ProviderId = providerResult.Provider?.Id,
                    ProviderVersionId = providerResult.Provider?.Id,
                    SpecificationId = providerResult.SpecificationId,
                    IsIndicativeProvider = providerResult.IsIndicativeProvider ?? false,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedAt = providerResult.CreatedAt == default ? DateTime.UtcNow : providerResult.CreatedAt.UtcDateTime
                };

                await _calculationResultsRepositoryPolicy.ExecuteAsync(() => _uow.GenericRepository<CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderResult>().Upsert(pr, x => x.ProviderResultId == pr.ProviderResultId));

                // Upsert Provider (basic fields)
                if (providerResult.Provider != null)
                {
                    var providerEntity = new CalculateFunding.Repositories.Common.EFCore.EntityModel.Provider
                    {
                        ProviderId = providerResult.Provider.Id,
                        ProviderVersionId = providerResult.Provider.Id,
                        Ukprn = providerResult.Provider.UKPRN,
                        Urn = providerResult.Provider.URN,
                        Upin = providerResult.Provider.UPIN,
                        EstablishmentNumber = providerResult.Provider.EstablishmentNumber,
                        DfeEstablishmentNumber = providerResult.Provider.DfeEstablishmentNumber,
                        Authority = providerResult.Provider.Authority,
                        ProviderType = providerResult.Provider.ProviderType,
                        ProviderSubType = providerResult.Provider.ProviderSubType,
                        DateOpened = providerResult.Provider.DateOpened?.UtcDateTime,
                        DateClosed = providerResult.Provider.DateClosed?.UtcDateTime,
                        ProviderProfileIdType = providerResult.Provider.ProviderProfileIdType,
                        LaCode = providerResult.Provider.LACode,
                        LaOrg = providerResult.Provider.LAOrg,
                        NavVendorNo = providerResult.Provider.NavVendorNo,
                        CrmAccountId = providerResult.Provider.CrmAccountId,
                        LegalName = providerResult.Provider.LegalName,
                        Status = providerResult.Provider.Status,
                        PhaseOfEducation = providerResult.Provider.PhaseOfEducation,
                        ReasonEstablishmentClosed = providerResult.Provider.ReasonEstablishmentClosed,
                        Successor = providerResult.Provider.Successor,
                        TrustStatus = providerResult.Provider.TrustStatus.ToString(),
                        TrustCode = providerResult.Provider.TrustCode,
                        TrustName = providerResult.Provider.TrustName,
                        Town = providerResult.Provider.Town,
                        Postcode = providerResult.Provider.Postcode,
                        CompaniesHouseNumber = providerResult.Provider.CompaniesHouseNumber,
                        GroupIdNumber = providerResult.Provider.GroupIdNumber,
                        RscRegionName = providerResult.Provider.RscRegionName,
                        RscRegionCode = providerResult.Provider.RscRegionCode,
                        GovernmentOfficeRegionName = providerResult.Provider.GovernmentOfficeRegionName,
                        GovernmentOfficeRegionCode = providerResult.Provider.GovernmentOfficeRegionCode,
                        DistrictName = providerResult.Provider.DistrictName,
                        DistrictCode = providerResult.Provider.DistrictCode,
                        WardCode = providerResult.Provider.WardCode,
                        WardName = providerResult.Provider.WardName,
                        CensusWardName = providerResult.Provider.CensusWardName,
                        CensusWardCode = providerResult.Provider.CensusWardCode,
                        MiddleSuperOutputAreaCode = providerResult.Provider.MiddleSuperOutputAreaCode,
                        MiddleSuperOutputAreaName = providerResult.Provider.MiddleSuperOutputAreaName,
                        LowerSuperOutputAreaCode = providerResult.Provider.LowerSuperOutputAreaCode,
                        LowerSuperOutputAreaName = providerResult.Provider.LowerSuperOutputAreaName,
                        ParliamentaryConstituencyCode = providerResult.Provider.ParliamentaryConstituencyCode,
                        ParliamentaryConstituencyName = providerResult.Provider.ParliamentaryConstituencyName,
                        LondonRegionCode = providerResult.Provider.LondonRegionCode,
                        LondonRegionName = providerResult.Provider.LondonRegionName,
                        CountyCode = providerResult.Provider.CountryCode,
                        CountyName = providerResult.Provider.CountryName,
                        LocalGovernmentGroupTypeCode = providerResult.Provider.LocalGovernmentGroupTypeCode,
                        LocalGovernmentGroupTypeName = providerResult.Provider.LocalGovernmentGroupTypeName,
                        Street = providerResult.Provider.Street,
                        Locality = providerResult.Provider.Locality,
                        Address3 = providerResult.Provider.Address3,
                        PaymentOrganisationIdentifier = providerResult.Provider.PaymentOrganisationIdentifier,
                        PaymentOrganisationName = providerResult.Provider.PaymentOrganisationName,
                        ProviderTypeCode = providerResult.Provider.ProviderTypeCode,
                        ProviderSubTypeCode = providerResult.Provider.ProviderSubTypeCode,
                        PreviousLaCode = providerResult.Provider.PreviousLaCode,
                        PreviousLaName = providerResult.Provider.PreviousLaName,
                        PreviousEstablishmentNumber = providerResult.Provider.PreviousEstablishmentNumber,
                        FurtherEducationTypeCode = providerResult.Provider.FurtherEducationTypeCode,
                        FurtherEducationTypeName = providerResult.Provider.FurtherEducationTypeName,
                        PhaseOfEducationCode = providerResult.Provider.PhaseOfEducationCode,
                        StatutoryLowAge = providerResult.Provider.StatutoryLowAge,
                        StatutoryHighAge = providerResult.Provider.StatutoryHighAge,
                        OfficialSixthFormCode = providerResult.Provider.OfficialSixthFormCode,
                        OfficialSixthFormName = providerResult.Provider.OfficialSixthFormName,
                        StatusCode = providerResult.Provider.StatusCode,
                        ReasonEstablishmentOpenedCode = providerResult.Provider.ReasonEstablishmentOpenedCode,
                        ReasonEstablishmentClosedCode = providerResult.Provider.ReasonEstablishmentClosedCode
                    };

                    await _calculationResultsRepositoryPolicy.ExecuteAsync(() => _uow.GenericRepository<CalculateFunding.Repositories.Common.EFCore.EntityModel.Provider>().Upsert(providerEntity, x => x.ProviderId == providerEntity.ProviderId && x.ProviderVersionId == providerEntity.ProviderVersionId));
                }

                if (providerResult.CalculationResults != null)
                {
                    foreach (var calcResult in providerResult.CalculationResults)
                    {
                        var cr = new CalculateFunding.Repositories.Common.EFCore.EntityModel.CalcResult
                        {
                            CalculationId = calcResult.Calculation?.Id,
                            CalculationName = calcResult.Calculation?.Name,
                            Value = calcResult.Value?.ToString(),
                            ExceptionType = calcResult.ExceptionType,
                            ExceptionMessage = calcResult.ExceptionMessage,
                            ExceptionStackTrace = calcResult.ExceptionStackTrace,
                            CalculationType = calcResult.CalculationType.ToString(),
                            CalculationDataType = calcResult.CalculationDataType.ToString(),
                            ProviderResultId = pr.ProviderResultId
                        };

                        await _calculationResultsRepositoryPolicy.ExecuteAsync(() => _uow.GenericRepository<CalculateFunding.Repositories.Common.EFCore.EntityModel.CalcResult>().Upsert(cr, x => x.CalculationId == cr.CalculationId && x.ProviderResultId == cr.ProviderResultId));
                    }
                }

                if (providerResult.FundingLineResults != null)
                {
                    foreach (var fundingLineResult in providerResult.FundingLineResults)
                    {
                        var fr = new CalculateFunding.Repositories.Common.EFCore.EntityModel.FundingLineResult
                        {
                            FundingLineId = fundingLineResult.FundingLine?.Id,
                            FundingLineName = fundingLineResult.FundingLine?.Name,
                            FundingLineFundingStreamId = fundingLineResult.FundingLineFundingStreamId,
                            Value = fundingLineResult.Value?.ToString(),
                            ExceptionType = fundingLineResult.ExceptionType,
                            ExceptionMessage = fundingLineResult.ExceptionMessage,
                            ExceptionStackTrace = fundingLineResult.ExceptionStackTrace,
                            ProviderResultId = pr.ProviderResultId
                        };

                        await _calculationResultsRepositoryPolicy.ExecuteAsync(() => _uow.GenericRepository<CalculateFunding.Repositories.Common.EFCore.EntityModel.FundingLineResult>().Upsert(fr, x => x.FundingLineId == fr.FundingLineId && x.ProviderResultId == fr.ProviderResultId));
                    }
                }
            }

            // commit once for the batch
            await _uow.CommitAsync();

            stopwatchInner.Stop();

            return stopwatchInner.ElapsedMilliseconds;
        }

        private async Task QueueMergeSpecificationJobsInBatches(IEnumerable<ProviderResult> providerResults,
            SpecificationSummary specification)
        {
            await _resultsApiClientPolicy.ExecuteAsync(() => _resultsApiClient.QueueMergeSpecificationInformationJob(new MergeSpecificationInformationRequest
            {
                SpecificationInformation = new SpecificationInformation
                {
                    Id = specification.Id,
                    Name = specification.Name,
                    FundingPeriodId = specification.FundingPeriod.Id,
                    FundingStreamIds = specification.FundingStreams?.Select(_ => _.Id).ToArray(),
                    LastEditDate = specification.LastEditedDate
                },
                ProviderIds = providerResults.Select(_ => _.Provider.Id).ToArray()
            }));
        }
    }
}
