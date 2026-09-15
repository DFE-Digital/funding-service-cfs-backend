using CalculateFunding.Common.ApiClient.Calcs;
using CalculateFunding.Common.ApiClient.Calcs.Models;
using CalculateFunding.Common.ApiClient.DataSets;
using CalculateFunding.Common.ApiClient.DataSets.Models;
using CalculateFunding.Common.ApiClient.FDS;
using CalculateFunding.Common.ApiClient.Models;
using CalculateFunding.Common.ApiClient.Specifications;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Migrations.Specification.Clone.Helpers;
using Polly;
using Serilog;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CalculateFunding.Migrations.Specification.Clone.Clones
{
    public class SourceApiClient : ISourceApiClient
    {
        private readonly ISpecificationsApiClient _specificationsApiClient;
        private readonly ICalculationsApiClient _calculationsApiClient;
        private readonly IDatasetsApiClient _datasetsApiClient;
        private readonly IFDSApiClient _fdsApiClient;

        private readonly AsyncPolicy _specificationsPolicy;
        private readonly AsyncPolicy _calcsPolicy;
        private readonly AsyncPolicy _datasetsPolicy;
        private readonly AsyncPolicy _fdsPolicy;

        private readonly ILogger _logger;

        public SourceApiClient(
            IBatchCloneResiliencePolicies batchCloneResiliencePolicies,
            ISpecificationsApiClient specificationsApiClient,
            ICalculationsApiClient calculationsApiClient,
            IDatasetsApiClient datasetsApiClient,
            IFDSApiClient fdsApiClient,
            ILogger logger)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));

            Guard.ArgumentNotNull(specificationsApiClient, nameof(specificationsApiClient));
            Guard.ArgumentNotNull(calculationsApiClient, nameof(calculationsApiClient));
            Guard.ArgumentNotNull(datasetsApiClient, nameof(datasetsApiClient));
            Guard.ArgumentNotNull(fdsApiClient, nameof(fdsApiClient));


            Guard.ArgumentNotNull(batchCloneResiliencePolicies, nameof(batchCloneResiliencePolicies));
            Guard.ArgumentNotNull(batchCloneResiliencePolicies.SpecificationsApiClient, nameof(batchCloneResiliencePolicies.SpecificationsApiClient));
            Guard.ArgumentNotNull(batchCloneResiliencePolicies.CalcsApiClient, nameof(batchCloneResiliencePolicies.CalcsApiClient));
            Guard.ArgumentNotNull(batchCloneResiliencePolicies.DatasetsApiClient, nameof(batchCloneResiliencePolicies.DatasetsApiClient));
            Guard.ArgumentNotNull(batchCloneResiliencePolicies.FdsApiClient, nameof(batchCloneResiliencePolicies.FdsApiClient));

            _logger = logger;

            _specificationsApiClient = specificationsApiClient;
            _calculationsApiClient = calculationsApiClient;
            _datasetsApiClient = datasetsApiClient;
            _fdsApiClient = fdsApiClient;

            _specificationsPolicy = batchCloneResiliencePolicies.SpecificationsApiClient;
            _calcsPolicy = batchCloneResiliencePolicies.CalcsApiClient;
            _datasetsPolicy = batchCloneResiliencePolicies.DatasetsApiClient;
            _fdsPolicy = batchCloneResiliencePolicies.FdsApiClient;
        }

        public async Task<SpecificationSummary> GetSpecificationSummaryById(string specificationId)
        {
            ApiResponse<SpecificationSummary> specificationSummaryResponse =
                await _specificationsPolicy.ExecuteAsync(() => _specificationsApiClient.GetSpecificationSummaryById(specificationId));
            specificationSummaryResponse.ValidateApiResponse(_logger, $"Error while retrieving SpecificationId={specificationId} summary.");
            return specificationSummaryResponse.Content;
        }

        public async Task<IEnumerable<Calculation>> GetCalculationsForSpecification(string specificationId)
        {
            ApiResponse<IEnumerable<Calculation>> calculationsResponse =
                await _calcsPolicy.ExecuteAsync(() => _calculationsApiClient.GetCalculationsForSpecification(specificationId));
            calculationsResponse.ValidateApiResponse(_logger, $"GetCalculationMetadataForSpecification operation failed for SpecificationId={specificationId}");
            return calculationsResponse.Content;
        }

        public async Task<IEnumerable<DatasetSpecificationRelationshipViewModel>> GetRelationshipsBySpecificationId(string specificationId)
        {
            ApiResponse<IEnumerable<DatasetSpecificationRelationshipViewModel>> datasetSpecificationRelationshipViewModelResponse =
                    await _datasetsPolicy.ExecuteAsync(() => _datasetsApiClient.GetRelationshipsBySpecificationId(specificationId));
            datasetSpecificationRelationshipViewModelResponse.ValidateApiResponse(_logger, $"GetRelationshipsBySpecificationId operation failed for SpecificationId={specificationId}");
            return datasetSpecificationRelationshipViewModelResponse.Content;
        }

        public async Task<IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>> GetFDSDataSchema(string fundingStreamId, string fundingPeriodId)
        {
            ApiResponse<IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>> fdsApiResponse = await _fdsPolicy.ExecuteAsync(() => _fdsApiClient.GetFDSDataSchema(fundingStreamId, fundingPeriodId));
            fdsApiResponse.ValidateApiResponse(_logger, $"Error while retrieving FDS data schema {fundingStreamId}, {fundingPeriodId}");
            return fdsApiResponse.Content;
        }

        public async Task<Common.ApiClient.FDS.Models.FDSDatasetDefinition> GetDatasetDefinition(string definitionId)
        {
            ApiResponse<Common.ApiClient.FDS.Models.FDSDatasetDefinition> fdsApiResponse = await _fdsPolicy.ExecuteAsync(() => _fdsApiClient.GetDatasetDefinition(definitionId));
            fdsApiResponse.ValidateApiResponse(_logger, $"Error while retrieving FDS data schema definition {definitionId}");
            return fdsApiResponse.Content;
        }
    }
}
