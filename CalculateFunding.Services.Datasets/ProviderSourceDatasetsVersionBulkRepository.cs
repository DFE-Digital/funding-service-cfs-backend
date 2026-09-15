using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Interfaces.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Datasets
{
    public class ProviderSourceDatasetsVersionBulkRepository<T> : IVersionBulkRepository<T> where T : VersionedItem
    {
        protected readonly IUnitOfWork _uow;

        public ProviderSourceDatasetsVersionBulkRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));
            _uow = uow;
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

        public async Task<int> GetNextVersionNumber(T version = null, int currentVersion = 0, string partitionKeyId = null, bool incrementFromCurrentVersion = false)
        {
            Guard.ArgumentNotNull(version, nameof(version));

            if (incrementFromCurrentVersion)
            {
                return currentVersion + 1;
            }

            var maxVersion = _uow.GenericRepository<EntityModel.ProviderSourceDatasetVersion>()
                .GetFirstAsQueryable(_ => _.ProviderSourceDatasetId == version.EntityId && !_.IsDeleted && _.IsLatest).Version;

            return maxVersion + 1;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProviderSourceDatasetBulkRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public Task<T> SaveVersion(T newVersion, string partitionKey)
        {
            throw new NotImplementedException();
        }

        public async Task<HttpStatusCode> SaveVersion(T newVersion)
        {
            throw new NotImplementedException();
        }

        public async Task<HttpStatusCode> SaveVersions(IEnumerable<T> newVersions)
        {
            Guard.ArgumentNotNull(newVersions, nameof(newVersions));

            var repo = _uow.GenericRepository<EntityModel.ProviderSourceDatasetVersion>();

            List<EntityModel.ProviderSourceDatasetVersion> updateExistingVersions = new List<EntityModel.ProviderSourceDatasetVersion>();
            List<EntityModel.ProviderSourceDatasetVersion> insertNewVersions = new List<EntityModel.ProviderSourceDatasetVersion>();

            foreach (var newVersion in newVersions)
            {
                ProviderSourceDatasetVersion providerSourceDatasetVersion = newVersion as ProviderSourceDatasetVersion;

                var latestproviderSourceDatasetVersion = repo.GetFirstAsQueryable(_ => _.ProviderSourceDatasetId == providerSourceDatasetVersion.ProviderSourceDatasetId && _.IsLatest && !_.IsDeleted);

                if (latestproviderSourceDatasetVersion != null)
                {
                    latestproviderSourceDatasetVersion.IsLatest = false;
                    latestproviderSourceDatasetVersion.UpdatedAt = DateTime.Now;
                    updateExistingVersions.Add(latestproviderSourceDatasetVersion);
                }

                EntityModel.ProviderSourceDatasetVersion newProviderSourceDatasetVersion = new EntityModel.ProviderSourceDatasetVersion()
                {
                    ProviderSourceDatasetVersionId = providerSourceDatasetVersion.Id,
                    ProviderSourceDatasetId = providerSourceDatasetVersion.ProviderSourceDatasetId,
                    DatasetId = providerSourceDatasetVersion.Dataset.Id,
                    DatasetName = providerSourceDatasetVersion.Dataset.Name,
                    DatasetVersion = providerSourceDatasetVersion.Dataset.Version,
                    ProviderSourceDatasetRowData = providerSourceDatasetVersion.Rows != null 
                    ? JsonConvert.SerializeObject(providerSourceDatasetVersion.Rows) : null,
                    Version = providerSourceDatasetVersion.Version,
                    AuthorId = providerSourceDatasetVersion.Author.Id,
                    AuthorName = providerSourceDatasetVersion.Author.Name,
                    Comment = providerSourceDatasetVersion.Comment,
                    PublishStatus = providerSourceDatasetVersion.PublishStatus.ToString(),
                    IsDeleted = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Date = DateTime.Now,
                    IsLatest = true
                };
                insertNewVersions.Add(newProviderSourceDatasetVersion);
            }

            if (updateExistingVersions.Any()) { repo.BulkUpdate(updateExistingVersions); }

            if (insertNewVersions.Any()) { repo.BulkInsertAsync(insertNewVersions); }

            await _uow.CommitAsync();

            return HttpStatusCode.Created;
        }
    }
}
