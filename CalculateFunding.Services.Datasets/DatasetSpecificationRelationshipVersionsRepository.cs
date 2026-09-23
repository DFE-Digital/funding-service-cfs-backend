using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Datasets
{
    public class DatasetSpecificationRelationshipVersionsRepository<T> : IVersionRepository<T> where T : VersionedItem
    {
        protected readonly IUnitOfWork _uow;

        public DatasetSpecificationRelationshipVersionsRepository(IUnitOfWork uow) {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
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

        public async Task<T> CreateVersion(T newVersion, T currentVersion, string partitionKey, bool incrementFromCurrentVersion)
        {
            Guard.ArgumentNotNull(newVersion, nameof(newVersion));

            newVersion.Date = DateTimeOffset.Now.ToLocalTime();

            if (currentVersion == null)
            {
                newVersion.Version = 1;

                newVersion.PublishStatus = PublishStatus.Draft;
            }
            else
            {
                newVersion.Version = await GetNextVersionNumber(newVersion, currentVersion.Version, partitionKey, incrementFromCurrentVersion);

                if (newVersion.PublishStatus == PublishStatus.Approved && (currentVersion.PublishStatus == PublishStatus.Draft || currentVersion.PublishStatus == PublishStatus.Updated))
                {
                    return newVersion;
                }

                switch (currentVersion.PublishStatus)
                {
                    case PublishStatus.Draft:
                        newVersion.PublishStatus = PublishStatus.Draft;
                        break;

                    case PublishStatus.Approved:
                        if (newVersion.PublishStatus != PublishStatus.Draft)
                        {
                            newVersion.PublishStatus = PublishStatus.Updated;
                        }

                        break;

                    default:
                        newVersion.PublishStatus = PublishStatus.Updated;
                        break;
                }
            }

            return newVersion;
        }

        public Task DeleteVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism)
        {
            throw new NotImplementedException();
        }

        public async Task<int> GetNextVersionNumber(T version, int currentVersion, string partitionKeyId, bool incrementFromCurrentVersion)
        {
            Guard.ArgumentNotNull(version, nameof(version));

            if (incrementFromCurrentVersion)
            {
                return currentVersion + 1;
            }

            var maxVersion = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>()
                .GetFirstAsQueryable(_ => _.DatasetSpecificationRelationshipId == version.EntityId && _.IsLatest && !_.IsDeleted).Version;

            return maxVersion + 1;
        }

        public Task<T> GetVersion(string entityId, int version)
        {
            throw new NotImplementedException();
        }

        public Task<int?> GetVersionCount(string entityId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetVersions(string entityId, string partitionKeyId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetVersions(string entityId, int? offset, int? limit)
        {
            throw new NotImplementedException();
        }

        public async Task<HttpStatusCode> SaveVersion(T newVersion)
        {
            Guard.ArgumentNotNull(newVersion, nameof(newVersion));

            DefinitionSpecificationRelationshipVersion relationshipVersion = newVersion as DefinitionSpecificationRelationshipVersion;

            var datasetSpecificationRelationshipRepo = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>();

            var datasetSpecificationRelationshipOldVersion = datasetSpecificationRelationshipRepo
                .GetFirstAsQueryable(_ => _.DatasetSpecificationRelationshipId == relationshipVersion.RelationshipId && _.IsLatest && !_.IsDeleted);

            if(datasetSpecificationRelationshipOldVersion != null)
            {
                datasetSpecificationRelationshipOldVersion.IsLatest = false;
                datasetSpecificationRelationshipOldVersion.LastUpdated = DateTime.Now;
                datasetSpecificationRelationshipOldVersion.UpdatedAt = DateTime.Now;
                datasetSpecificationRelationshipRepo.Update(datasetSpecificationRelationshipOldVersion);
            }

            var datasetSpecificationRelationship = new EntityModel.DatasetSpecificationRelationship()
            {
                DatasetSpecificationRelationshipVersionId = relationshipVersion.Id,
                DatasetSpecificationRelationshipId = relationshipVersion.RelationshipId,
                Name = relationshipVersion.Name,
                DatasetDefinitionId = relationshipVersion.DatasetDefinition?.Id,
                DatasetDefinitionName = relationshipVersion.DatasetDefinition?.Name,
                SpecificationId = relationshipVersion.Specification.Id,
                Description = relationshipVersion.Description,
                DatasetId = relationshipVersion.DatasetVersion?.Id,
                DatasetVersionNumber = relationshipVersion.DatasetVersion?.Version,
                IsSetAsProviderData = relationshipVersion.IsSetAsProviderData,
                ConverterEnabled = relationshipVersion.ConverterEnabled,
                UsedInDataAggregations = relationshipVersion.UsedInDataAggregations,
                LastUpdated = relationshipVersion.LastUpdated?.DateTime,
                RelationshipType = relationshipVersion.RelationshipType.ToString(),
                PublishedSpecificationConfiguration = relationshipVersion.PublishedSpecificationConfiguration != null
                    ? JsonConvert.SerializeObject(relationshipVersion.PublishedSpecificationConfiguration) : null,
                Version = relationshipVersion.Version,
                AuthorId = relationshipVersion.Author.Id,
                AuthorName = relationshipVersion.Author.Name,
                Comment = relationshipVersion.Comment,
                PublishStatus = relationshipVersion.PublishStatus.ToString(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false,
                IsLatest = true,
                PublishedSpecificationId = relationshipVersion.PublishedSpecificationConfiguration != null
                    ? relationshipVersion.PublishedSpecificationConfiguration.SpecificationId : null,
                FundingPeriodName = relationshipVersion.FundingPeriodName
            };

            datasetSpecificationRelationshipRepo.Insert(datasetSpecificationRelationship);

            await _uow.CommitAsync();

            return HttpStatusCode.Created;

        }

        public Task SaveVersion(T newVersion, string partitionKey)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersions(IEnumerable<T> newVersions, int maxDegreesOfParallelism)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetAllVersions()
        {
            throw new NotImplementedException();
        }
    }
}
