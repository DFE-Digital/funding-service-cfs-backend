using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Models.Versioning;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Messages;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EfcoreProviderSourceDataset = CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderSourceDataset;
using EfcoreProviderSourceDatasetVersion = CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderSourceDatasetVersion;
using ProviderSourceDataset = CalculateFunding.Models.Datasets.ProviderSourceDataset;
using ProviderSourceDatasetVersion = CalculateFunding.Models.Datasets.ProviderSourceDatasetVersion;

namespace CalculateFunding.Services.Results.Repositories
{
    public class ProviderSourceDatasetRepository : IProviderSourceDatasetRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;
        public ProviderSourceDatasetRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }
        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProviderSourceDatasetRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public async Task DeleteProviderSourceDataset(string providerSourceDatasetId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(providerSourceDatasetId, nameof(providerSourceDatasetId));
            Guard.ArgumentNotNull(deletionType, nameof(deletionType));

            var providerSourceDatasetList = await _uow.GenericRepository<EfcoreProviderSourceDataset>()
                .GetManyAsQueryable(x => x.ProviderSourceDatasetId == providerSourceDatasetId)
                .ToListAsync();

            if (providerSourceDatasetList.IsNullOrEmpty())
                return;

            foreach (var providerDataset in providerSourceDatasetList)
            {
                if (deletionType == DeletionType.PermanentDelete)
                {
                   _uow.GenericRepository<EfcoreProviderSourceDataset>().Delete(providerDataset);
                }
                else if (deletionType == DeletionType.SoftDelete)
                {
                    providerDataset.IsDeleted = true;
                    _uow.GenericRepository<EfcoreProviderSourceDataset>().Update(providerDataset);                    
                }                
            }  
            await _uow.CommitAsync();           
        }

        public async Task DeleteProviderSourceDatasetVersion(string providerSourceDatasetVersionId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(providerSourceDatasetVersionId, nameof(providerSourceDatasetVersionId));
            Guard.ArgumentNotNull(_uow, nameof(_uow));

            var providerSourceDatasetVersions = await _uow.GenericRepository<EfcoreProviderSourceDatasetVersion>()
                .GetManyAsQueryable(x => x.ProviderSourceDatasetVersionId == providerSourceDatasetVersionId)
                .ToListAsync();

            if (providerSourceDatasetVersions.IsNullOrEmpty()) return;

            foreach (var providerSourceDatasetVersion in providerSourceDatasetVersions)
            {
                if (deletionType == DeletionType.PermanentDelete)
                {
                    _uow.GenericRepository<EfcoreProviderSourceDatasetVersion>().Delete(providerSourceDatasetVersion);
                }
                else if (deletionType == DeletionType.SoftDelete)
                {
                    providerSourceDatasetVersion.IsDeleted = true;
                    providerSourceDatasetVersion.UpdatedAt = DateTime.UtcNow;
                    _uow.GenericRepository<EfcoreProviderSourceDatasetVersion>()
                        .Update(providerSourceDatasetVersion);
                }
            }
            await _uow.CommitAsync();
        }

        public async Task<IEnumerable<string>> GetAllScopedProviderIdsForSpecificationId(string specificationId)
        { 
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            return await _uow.GenericRepository<EfcoreProviderSourceDataset>()
                .GetManyAsQueryable(x => x.SpecificationId == specificationId && x.DefinesScope == true && x.IsDeleted == false)
                .Select(x => x.ProviderId)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProviderSourceDataset>> GetProviderSourceDatasets(string providerId, string specificationId)
        { 
            Guard.IsNullOrWhiteSpace(providerId, nameof(providerId));
            Guard.IsNullOrWhiteSpace(specificationId, nameof(specificationId));

            var providerSourceDatasetList = new List<ProviderSourceDataset>();

            var providerSourceDatasets = await _uow.GenericRepository<EfcoreProviderSourceDataset>()
                .GetManyAsQueryable(x => x.ProviderId == providerId && x.SpecificationId == specificationId && x.IsDeleted == false)
                .ToListAsync();

            foreach (var providerSourceDataset in providerSourceDatasets)
            {
                var providerSourceDatasetVersion = await _uow.GenericRepository<EfcoreProviderSourceDatasetVersion>()
                    .FirstOrDefaultAsync(x => x.ProviderSourceDatasetId == providerSourceDataset.ProviderSourceDatasetId && x.IsDeleted == false);

                providerSourceDatasetList.Add(
                   new ProviderSourceDataset
                   {
                       SpecificationId = providerSourceDataset.SpecificationId,
                       ProviderId = providerSourceDataset.ProviderId,
                       DataDefinitionId = providerSourceDataset.DataDefinitionId,
                       DataRelationship = new Reference()
                       {
                           Id = providerSourceDataset.DatasetRelationshipSummaryId,
                           Name = providerSourceDataset.DataRelationshipSummaryName
                       },
                       DatasetRelationshipSummary = new Reference()
                       {
                           Id = providerSourceDataset.DatasetRelationshipSummaryId,
                           Name = providerSourceDataset.DataRelationshipSummaryName
                       },
                       DatasetRelationshipType = Enum.Parse<DatasetRelationshipType>(providerSourceDataset.DatasetRelationshipType),
                       DataGranularity = (DataGranularity)Enum.Parse<DataGranularity>(providerSourceDataset.DataGranularity),
                       DefinesScope = (bool)providerSourceDataset.DefinesScope,

                       Current = new ProviderSourceDatasetVersion()
                       {
                           ProviderSourceDatasetId = providerSourceDataset.ProviderSourceDatasetId,
                           Dataset = new CalculateFunding.Models.VersionReference(providerSourceDatasetVersion.DatasetId, providerSourceDatasetVersion.DatasetName, providerSourceDatasetVersion.DatasetVersion),

                           Rows = providerSourceDatasetVersion.ProviderSourceDatasetRowData != null
                       ? JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(providerSourceDatasetVersion.ProviderSourceDatasetRowData)
                       : new List<Dictionary<string, object>>(),

                           ProviderId = providerSourceDataset.ProviderId,
                           Version = providerSourceDatasetVersion.Version,
                           Date = providerSourceDatasetVersion.Date,
                           Author = new Reference()
                           {
                               Id = providerSourceDatasetVersion.AuthorId,
                               Name = providerSourceDatasetVersion.AuthorName
                           },
                           Comment = providerSourceDatasetVersion.Comment,
                           PublishStatus = (CalculateFunding.Models.Versioning.PublishStatus)Enum.Parse<PublishStatus>(providerSourceDatasetVersion.PublishStatus)
                       }
                   }
               );
            }
            return providerSourceDatasetList;
        }
    }
}
