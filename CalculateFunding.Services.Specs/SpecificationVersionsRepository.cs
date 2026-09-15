using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Specs;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using System.Linq;
using PublishStatus = CalculateFunding.Models.Versioning.PublishStatus;

namespace CalculateFunding.Services.Specs
{
    public class SpecificationVersionsRepository<T> : IVersionRepository<T> where T : VersionedItem
    {
        protected readonly IUnitOfWork _uow;
        public SpecificationVersionsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(SpecificationsRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }


        public async Task<T> CreateVersion(T newVersion, T currentVersion = null, string partitionKey = null, bool incrementFromCurrentVersion = false)
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

        public Task DeleteVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism = 30)
        {
            throw new NotImplementedException();
        }

        public async Task<int> GetNextVersionNumber(T version = null, int currentVersion = 0, string partitionKeyId = null, bool incrementFromCurrentVersion = false)
        {
            Guard.ArgumentNotNull(version, nameof(version));

            if (incrementFromCurrentVersion)
            {
                return currentVersion + 1;
            }

            var maxVersion = _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetSingleAsQueryable(_ => _.SpecificationId == version.EntityId && !_.IsDeleted && _.IsLatest).Version;

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

        public Task<IEnumerable<T>> GetVersions(string entityId, string partitionKeyId = null)
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

            SpecificationVersion specificationVersion = newVersion as SpecificationVersion;

            var specVersionRepo = _uow.GenericRepository<EntityModel.SpecificationVersion>();
            var relationshipRepo = _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>();
            var variationPointerRepo = _uow.GenericRepository<EntityModel.VariationPointer>();

            var latestSpecificationVersion = specVersionRepo.GetFirstAsQueryable(_ => _.SpecificationId == specificationVersion.SpecificationId && _.IsLatest && !_.IsDeleted);

            if (latestSpecificationVersion != null)
            {
                latestSpecificationVersion.IsLatest = false;
                latestSpecificationVersion.UpdatedAt = specificationVersion.Date.DateTime;
                specVersionRepo.Update(latestSpecificationVersion);
            }

            EntityModel.SpecificationVersion newSpecificationVersion = new EntityModel.SpecificationVersion()
            {
                SpecificationVersionId = specificationVersion.Id,
                SpecificationName = specificationVersion.Name,
                SpecificationId = specificationVersion.SpecificationId,
                PublishStatus = specificationVersion.PublishStatus.ToString(),
                ExternalPublicationDate = specificationVersion.ExternalPublicationDate.GetValueOrDefault().DateTime,
                AuthorId = specificationVersion.Author.Id,
                AuthorName = specificationVersion.Author.Name,
                Comment = specificationVersion.Comment,
                Description = specificationVersion.Description,
                IsDeleted = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                CoreProviderVersionUpdates = specificationVersion.CoreProviderVersionUpdates.ToString(),
                ProviderSource = specificationVersion.ProviderSource.ToString(),
                ProviderSnapshotId = specificationVersion.ProviderSnapshotId ?? null,
                EarliestPaymentAvailableDate = specificationVersion.EarliestPaymentAvailableDate.GetValueOrDefault().DateTime,
                Version = specificationVersion.Version,
                ProviderVersionId = specificationVersion.ProviderVersionId,
                TemplateVersion = decimal.Parse(specificationVersion.TemplateIds.First().Value),
                Date = DateTime.Now,
                IsLatest = true
            };

            specVersionRepo.Insert(newSpecificationVersion);

            specificationVersion.DataDefinitionRelationshipIds.ForEach(_ =>
            {
                EntityModel.DefinitionSpecificationRelationship definitionSpecificationRelationship = new EntityModel.DefinitionSpecificationRelationship()
                {
                    DataDefinitionRelationshipId = _,
                    SpecificationVersionId = specificationVersion.Id,
                };
                relationshipRepo.Insert(definitionSpecificationRelationship);
            });

            specificationVersion.ProfileVariationPointers.ForEach(_ =>
            {
                EntityModel.VariationPointer variationPointer = new EntityModel.VariationPointer()
                {
                    FundingLineId = _.FundingLineId,
                    PeriodType = _.PeriodType,
                    PeriodValue = _.TypeValue,
                    Year = _.Year,
                    Occurrence = _.Occurrence,
                    SpecificationVersionId = specificationVersion.Id
                };
                variationPointerRepo.Insert(variationPointer);
            });

            await _uow.CommitAsync();

            return HttpStatusCode.Created;

        }

        public Task SaveVersion(T newVersion, string partitionKey)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersions(IEnumerable<T> newVersions, int maxDegreesOfParallelism = 30)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism = 30)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetAllVersions()
        {
            throw new NotImplementedException();
        }
    }
}
