using CalculateFunding.Common.ApiClient.Calcs.Models;
using CalculateFunding.Common.ApiClient.DataSets.Models;
using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CalculateFunding.Migrations.Specification.Clone.Clones
{
    public interface ISourceApiClient
    {
        Task<SpecificationSummary> GetSpecificationSummaryById(string specificationId);

        Task<IEnumerable<Calculation>> GetCalculationsForSpecification(string specificationId);

        Task<IEnumerable<DatasetSpecificationRelationshipViewModel>> GetRelationshipsBySpecificationId(string specificationId);

        Task<IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>> GetFDSDataSchema(string fundingStream, string fundingPeriod);

        Task<FDSDatasetDefinition> GetDatasetDefinition(string definitionId);
    }
}