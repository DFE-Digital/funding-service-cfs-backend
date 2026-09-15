using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Publishing.Interfaces;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing
{
    public class PostReleaseJobCreationService : IPostReleaseJobCreationService
    {
        private readonly ICreatePublishDatasetsDataCopyJob _createPublishDatasetsDataCopyJob;
        private readonly ICreateProcessDatasetObsoleteItemsJob _createProcessDatasetObsoleteItemsJob;

        public PostReleaseJobCreationService(
            ICreatePublishDatasetsDataCopyJob createPublishDatasetsDataCopyJob,
            ICreateProcessDatasetObsoleteItemsJob createProcessDatasetObsoleteItemsJob)
        {
            Guard.ArgumentNotNull(createPublishDatasetsDataCopyJob, nameof(createPublishDatasetsDataCopyJob));
            Guard.ArgumentNotNull(createProcessDatasetObsoleteItemsJob, nameof(createProcessDatasetObsoleteItemsJob));

            _createPublishDatasetsDataCopyJob = createPublishDatasetsDataCopyJob;
            _createProcessDatasetObsoleteItemsJob = createProcessDatasetObsoleteItemsJob;
        }
        public async Task QueueJobs(SpecificationSummary specification, string correlationId, Reference author)
        {
            Guard.ArgumentNotNull(specification, nameof(specification));

            await _createPublishDatasetsDataCopyJob.CreateJob(specification.Id, author, correlationId);

            await _createProcessDatasetObsoleteItemsJob.CreateJob(specification.Id, author, correlationId);
        }
    }
}
