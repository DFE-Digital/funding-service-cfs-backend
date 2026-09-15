using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Messages;
using CalculateFunding.Services.Datasets.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using System.Collections.Concurrent;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Versioning;
using System.Text.Json.Nodes;
using Newtonsoft.Json;

namespace CalculateFunding.Services.Datasets
{
    public class ProviderSourceDatasetsRepository : IProviderSourceDatasetsRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;

        public ProviderSourceDatasetsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));
            _uow = uow; 
        }

        public Task DeleteCurrentProviderSourceDatasets(IEnumerable<ProviderSourceDataset> providerSourceDatasets)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteProviderSourceDataset(string dataDefinitionRelationshipId, DeletionType deletionType)
        {          
            var providerSourceDatasetRepo = _uow.GenericRepository<EntityModel.ProviderSourceDataset>();
            var providerSourceDatasets = await providerSourceDatasetRepo.GetManyAsQueryable(_ => _.DatasetRelationshipSummaryId == dataDefinitionRelationshipId && !_.IsDeleted).ToListAsync();

            if (providerSourceDatasets.Any()) {

                foreach (var providerSourceDataset in providerSourceDatasets)
                {
                    if (deletionType == DeletionType.SoftDelete)
                    {
                        if (providerSourceDataset.IsDeleted == false)
                        {
                            providerSourceDataset.IsDeleted = true;
                            providerSourceDatasetRepo.Update(providerSourceDataset);
                        }
                    }

                    if (deletionType == DeletionType.PermanentDelete)
                    {
                        providerSourceDatasetRepo.Delete(providerSourceDataset);
                    }

                }
            }
            
        }

        public async Task DeleteProviderSourceDatasetVersion(string dataDefinitionRelationshipId, DeletionType deletionType)
        {
            var providerSourceDatasetRepo = _uow.GenericRepository<EntityModel.ProviderSourceDataset>();
            var providerSourceDatasetVersionRepo = _uow.GenericRepository<EntityModel.ProviderSourceDatasetVersion>();
            var providerSourceDatasets = await providerSourceDatasetRepo.GetManyAsQueryable(_ => _.DatasetRelationshipSummaryId == dataDefinitionRelationshipId && !_.IsDeleted).ToListAsync();

            if (providerSourceDatasets.Any()) {

                foreach (var providerSourceDataset in providerSourceDatasets)
                {
                    var providerSourceVersionDatasetVersions = await providerSourceDatasetVersionRepo.GetManyAsQueryable(_ => _.ProviderSourceDatasetId == providerSourceDataset.ProviderSourceDatasetId && !_.IsDeleted).ToListAsync();

                    if (!providerSourceVersionDatasetVersions.Any())
                        continue;

                    if (deletionType == DeletionType.SoftDelete)
                    {
                        foreach (var providerSourceVersion in providerSourceVersionDatasetVersions)
                        {
                            providerSourceVersion.IsDeleted = true;
                            providerSourceVersion.UpdatedAt = DateTime.Now;
                            providerSourceDatasetVersionRepo.Update(providerSourceVersion);
                        }
                    }

                    if (deletionType == DeletionType.PermanentDelete)
                    {
                        providerSourceDatasetVersionRepo.Delete(providerSourceVersionDatasetVersions);
                    }
                }

            }
            
        }

        public async Task<IEnumerable<DocumentEntity<ProviderSourceDataset>>> GetCurrentProviderSourceDatasets(string specificationId, string relationshipId)
        {
            List<EntityModel.ProviderSourceDataset> providerSourceDatasets = await _uow.GenericRepository<EntityModel.ProviderSourceDataset>().GetManyAsQueryable(_ => _.SpecificationId == specificationId && _.DatasetRelationshipSummaryId == relationshipId).ToListAsync();

            if(!providerSourceDatasets.IsNullOrEmpty())
            {
                var providerSourceDatasetIds = providerSourceDatasets.Select(_ => _.ProviderSourceDatasetId).ToList();
                var providerSourceDatasetVersions = await _uow.GenericRepository<EntityModel.ProviderSourceDatasetVersion>().GetManyAsQueryable(_ 
                    => providerSourceDatasetIds.Contains(_.ProviderSourceDatasetId) && _.IsLatest).ToListAsync();

                var providerSourceDatasetList = new ConcurrentBag<DocumentEntity<ProviderSourceDataset>>();
                Parallel.ForEach(providerSourceDatasets, providerSourceDataset =>
                {
                    var providerSourceDatasetVersion = providerSourceDatasetVersions.Where(_ => _.ProviderSourceDatasetId == providerSourceDataset.ProviderSourceDatasetId).FirstOrDefault();
                    var documentEntity = new DocumentEntity<ProviderSourceDataset>
                      {
                          DocumentType = nameof(ProviderSourceDataset),
                          Content = BuildDataset(providerSourceDataset, providerSourceDatasetVersion),
                          CreatedAt = providerSourceDatasetVersion.CreatedAt,
                          UpdatedAt = providerSourceDatasetVersion.UpdatedAt,
                          Deleted = providerSourceDataset.IsDeleted,
                      };
                     providerSourceDatasetList.Add(documentEntity);
                });

                return providerSourceDatasetList;
            }
            return Enumerable.Empty<DocumentEntity<ProviderSourceDataset>>();           
        }

        public bool IsConnectedToSQL()
        {
            return true;
        }


        private ProviderSourceDataset BuildDataset(EntityModel.ProviderSourceDataset providerSourceDataset, EntityModel.ProviderSourceDatasetVersion providerSourceDatasetVersion)
        {
            return new ProviderSourceDataset()
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
                DataGranularity = Enum.Parse<DataGranularity>(providerSourceDataset.DataGranularity),
                DefinesScope = (bool)providerSourceDataset.DefinesScope,
                Current = new ProviderSourceDatasetVersion()
                {
                    ProviderSourceDatasetId = providerSourceDataset.ProviderSourceDatasetId,
                    Dataset = new Models.VersionReference(providerSourceDatasetVersion.DatasetId, providerSourceDatasetVersion.DatasetName, providerSourceDatasetVersion.DatasetVersion),

                    Rows = providerSourceDatasetVersion.ProviderSourceDatasetRowData != null
                    ? JsonConvert.DeserializeObject<List<Dictionary<string,object>>>(providerSourceDatasetVersion.ProviderSourceDatasetRowData)
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
                    PublishStatus = Enum.Parse<PublishStatus>(providerSourceDatasetVersion.PublishStatus)
                }               
            };
        }


        public async Task<IEnumerable<ProviderSourceDatasetHistory>> GetProviderSourceDatasetHistories(string specificationId, string relationshipId)
        {
            throw new NotImplementedException();
        }
        

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(DataSetsRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public Task UpdateCurrentProviderSourceDatasets(IEnumerable<ProviderSourceDataset> providerSourceDatasets)
        {
            throw new NotImplementedException();
        }

        public Task UpdateProviderSourceDatasetHistory(IEnumerable<ProviderSourceDatasetHistory> providerSourceDatasets)
        {
            throw new NotImplementedException();
        }
    }
}
