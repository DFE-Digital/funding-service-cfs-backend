using System.Collections.Generic;
using System.Threading.Tasks;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Common.Utility;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Common.EfCore.UnitOfWork;
using System;

namespace CalculateFunding.Services.Datasets
{
    public class ProviderSourceDatasetBulkRepository : IProviderSourceDatasetBulkRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProviderSourceDatasetBulkRepository(IUnitOfWork unitOfWork)
        {
            Guard.ArgumentNotNull(unitOfWork, nameof(unitOfWork));
            _unitOfWork = unitOfWork;
        }

        public async Task DeleteCurrentProviderSourceDatasets(IEnumerable<ProviderSourceDataset> providerSourceDatasets)
        {
            Guard.ArgumentNotNull(providerSourceDatasets, nameof(providerSourceDatasets));
            var repo = _unitOfWork.GenericRepository<EntityModel.ProviderSourceDataset>();

            foreach (var providerSourceDataset in providerSourceDatasets)
            {
                var existingProviderSourceDataset = await repo.SingleOrDefaultAsync(_ => _.ProviderSourceDatasetId == providerSourceDataset.Id && !_.IsDeleted);
                if (existingProviderSourceDataset != null)
                {
                    existingProviderSourceDataset.IsDeleted = true;
                    repo.Update(existingProviderSourceDataset);
                }
            }
        }

        public async Task UpdateCurrentProviderSourceDatasets(IEnumerable<ProviderSourceDataset> providerSourceDatasets)
        {
            Guard.ArgumentNotNull(providerSourceDatasets, nameof(providerSourceDatasets));

            var repo = _unitOfWork.GenericRepository<EntityModel.ProviderSourceDataset>();

            foreach (var providerSourceDataset in providerSourceDatasets)
            {
                var existingProviderSourceDataset = repo.GetFirstAsQueryable(_ => _.ProviderSourceDatasetId == providerSourceDataset.Id);
                if (existingProviderSourceDataset == null)
                {
                    var NewProviderSourceDataset = new EntityModel.ProviderSourceDataset()
                    {
                        ProviderSourceDatasetId = providerSourceDataset.Id,
                        ProviderId = providerSourceDataset.ProviderId,
                        SpecificationId = providerSourceDataset.SpecificationId,
                        DatasetRelationshipSummaryId = providerSourceDataset.DatasetRelationshipSummary.Id,
                        DataRelationshipSummaryName = providerSourceDataset.DatasetRelationshipSummary.Name,
                        DataGranularity = providerSourceDataset.DataGranularity.ToString(),
                        DefinesScope = providerSourceDataset.DefinesScope,
                        DatasetRelationshipType = providerSourceDataset.DatasetRelationshipType.ToString(),
                        DataDefinitionId = providerSourceDataset.DataDefinitionId,
                        IsDeleted = false,

                    };
                    repo.Insert(NewProviderSourceDataset);
                }
                else
                {
                    existingProviderSourceDataset.DatasetRelationshipSummaryId = providerSourceDataset.DatasetRelationshipSummary.Id;
                    existingProviderSourceDataset.DataRelationshipSummaryName = providerSourceDataset.DatasetRelationshipSummary.Name;
                    existingProviderSourceDataset.DataGranularity = providerSourceDataset.DataGranularity.ToString();
                    existingProviderSourceDataset.DefinesScope = providerSourceDataset.DefinesScope;
                    existingProviderSourceDataset.DatasetRelationshipType = providerSourceDataset.DatasetRelationshipType.ToString();
                    existingProviderSourceDataset.DataDefinitionId = providerSourceDataset.DataDefinitionId;
                    existingProviderSourceDataset.IsDeleted = false;
                    repo.Update(existingProviderSourceDataset);
                }
            }
        }

        public async Task UpdateProviderSourceDatasetHistory(IEnumerable<ProviderSourceDatasetHistory> providerSourceDatasetHistories)
        {
            throw new NotImplementedException();
          
        }
    }
}
