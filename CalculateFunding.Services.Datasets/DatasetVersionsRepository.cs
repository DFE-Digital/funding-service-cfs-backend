using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Model = CalculateFunding.Models.Datasets;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using CalculateFunding.Models.Datasets;

namespace CalculateFunding.Services.Datasets
{
    public class DatasetVersionsRepository<T> : IVersionRepository<T> where T : VersionedItem
    {

        protected readonly IUnitOfWork _uow;
        public DatasetVersionsRepository(IUnitOfWork uow)
        {
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

            var maxVersion = _uow.GenericRepository<EntityModel.DatasetVersion>()
                .GetFirstAsQueryable(_ => _.DatasetId == version.EntityId && _.IsLatest && !_.IsDeleted).Version;

            return maxVersion + 1;
        }

        public async Task<T> GetVersion(string entityId, int version)
        {
            Guard.ArgumentNotNull(entityId, nameof(entityId));
            Guard.ArgumentNotNull(version, nameof(version));

            var datasetVersionObj = _uow.GenericRepository<EntityModel.DatasetVersion>().GetFirstAsQueryable(_ => _.DatasetId == entityId 
            && _.Version == version && !_.IsDeleted);

            var datasetVersion = BuildDatasetVersion(datasetVersionObj);

            return datasetVersion as T; 
        }

        public async Task<int?> GetVersionCount(string entityId)
        {
            Guard.ArgumentNotNull(entityId, nameof(entityId));

            return await _uow.GenericRepository<EntityModel.DatasetVersion>().GetManyAsQueryable(_ => _.DatasetId.Equals(entityId) && !_.IsDeleted).CountAsync();
        }

        public async Task<IEnumerable<T>> GetVersions(string entityId, string partitionKeyId)
        {
            Guard.ArgumentNotNull(entityId, nameof(entityId));

            IEnumerable<EntityModel.DatasetVersion> datasetVersions = await _uow.GenericRepository<EntityModel.DatasetVersion>()
                .GetManyAsQueryable(_ => _.DatasetId.Equals(entityId) && !_.IsDeleted).ToListAsync();

            if (!datasetVersions.Any()) { return Enumerable.Empty<T>(); }

            return (IEnumerable<T>)datasetVersions.Select(_ => BuildDatasetVersion(_));
        }

        public async Task<IEnumerable<T>> GetAllVersions()
        {
            IEnumerable<EntityModel.DatasetVersion> datasetVersions = await _uow.GenericRepository<EntityModel.DatasetVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted).ToListAsync();

            var fundingStreamIds = datasetVersions.Select(_ => _.FundingStreamId).ToList();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>().GetManyAsQueryable(_ => fundingStreamIds.Contains(_.FundingStreamId)).ToListAsync();

            var datasetVersionList = new ConcurrentBag<DatasetVersion>();

            Parallel.ForEach(datasetVersions, datasetversion =>
            {
                var version = BuildDatasetVersion(datasetversion, fundingStreams.FirstOrDefault(x => x.FundingStreamId == datasetversion.FundingStreamId));
                datasetVersionList.Add(version);
            });

            return (IEnumerable<T>)datasetVersionList;
        }

        public async Task<IEnumerable<T>> GetVersions(string entityId, int? offset, int? limit)
        {
            Guard.ArgumentNotNull(entityId, nameof(entityId));

            IEnumerable<EntityModel.DatasetVersion> datasetVersions = await _uow.GenericRepository<EntityModel.DatasetVersion>()
                .GetManyAsQueryable(_ => _.DatasetId.Equals(entityId) && !_.IsDeleted).OrderByDescending(_ => _.Version).Skip(offset.GetValueOrDefault()).Take(limit.GetValueOrDefault()).ToListAsync();

            if (!datasetVersions.Any()) { return Enumerable.Empty<T>(); }

            return (IEnumerable<T>) datasetVersions.Select(_ => BuildDatasetVersion(_));
        }

        public async Task<HttpStatusCode> SaveVersion(T newVersion)
        {
            Guard.ArgumentNotNull(newVersion, nameof(newVersion));

            Model.DatasetVersion datasetNewVersion = newVersion as Model.DatasetVersion;

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirstAsQueryable(_ => _.FundingStreamCode == datasetNewVersion.FundingStream.Id);

            var datasetVersionRepo = _uow.GenericRepository<EntityModel.DatasetVersion>();

            var existingDatasetVersion = datasetVersionRepo.GetFirstAsQueryable(_ => _.DatasetVersionId == datasetNewVersion.Id);

            if (existingDatasetVersion != null)
            {
                existingDatasetVersion.BlobName = datasetNewVersion.BlobName;
                existingDatasetVersion.RowCount = datasetNewVersion.RowCount;
                existingDatasetVersion.NewRowCount = datasetNewVersion.NewRowCount;
                existingDatasetVersion.AmendedRowCount = datasetNewVersion.AmendedRowCount;
                existingDatasetVersion.UploadedBlobFilePath = datasetNewVersion.UploadedBlobFilePath;
                existingDatasetVersion.ChangeType = datasetNewVersion.ChangeType.ToString();
                existingDatasetVersion.FundingStreamId = fundingStream.FundingStreamId;
                existingDatasetVersion.ProviderVersionId = datasetNewVersion.ProviderVersionId;
                existingDatasetVersion.Description = datasetNewVersion.Description;
                existingDatasetVersion.Date = datasetNewVersion.Date.DateTime;
                existingDatasetVersion.AuthorId = datasetNewVersion.Author.Id;
                existingDatasetVersion.AuthorName = datasetNewVersion.Author.Name;
                existingDatasetVersion.Comment = datasetNewVersion.Comment;
                existingDatasetVersion.PublishStatus = datasetNewVersion.PublishStatus.ToString();
                existingDatasetVersion.UpdatedAt = DateTime.Now;

                datasetVersionRepo.Update(existingDatasetVersion);
            }else
            {
                var datasetOldVersion = datasetVersionRepo.GetFirstAsQueryable(_ => _.DatasetId == datasetNewVersion.DatasetId && _.IsLatest && !_.IsDeleted);

                if (datasetOldVersion != null)
                {
                    datasetOldVersion.IsLatest = false;
                    datasetOldVersion.UpdatedAt = DateTime.Now;
                    datasetVersionRepo.Update(datasetOldVersion);
                }

                var datasetVersion = new EntityModel.DatasetVersion()
                {
                    DatasetVersionId = datasetNewVersion.Id,
                    DatasetId = datasetNewVersion.DatasetId,
                    BlobName = datasetNewVersion.BlobName,
                    RowCount = datasetNewVersion.RowCount,
                    NewRowCount = datasetNewVersion.NewRowCount,
                    AmendedRowCount = datasetNewVersion.AmendedRowCount,
                    UploadedBlobFilePath = datasetNewVersion.UploadedBlobFilePath,
                    ChangeType = datasetNewVersion.ChangeType.ToString(),
                    FundingStreamId = fundingStream.FundingStreamId,
                    ProviderVersionId = datasetNewVersion.ProviderVersionId,
                    Description = datasetNewVersion.Description,
                    Version = datasetNewVersion.Version,
                    Date = datasetNewVersion.Date.DateTime,
                    AuthorId = datasetNewVersion.Author.Id,
                    AuthorName = datasetNewVersion.Author.Name,
                    Comment = datasetNewVersion.Comment,
                    PublishStatus = datasetNewVersion.PublishStatus.ToString(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false,
                    IsLatest = true
                };

                datasetVersionRepo.Insert(datasetVersion);
            }

            await _uow.CommitAsync();

            return HttpStatusCode.Created;
        }

        public Task SaveVersion(T newVersion, string partitionKey)
        {
            throw new NotImplementedException();
        }

        public async Task SaveVersions(IEnumerable<T> newVersions, int maxDegreesOfParallelism)
        {
            Guard.ArgumentNotNull(newVersions, nameof(newVersions));

            List<EntityModel.DatasetVersion> updateExistingVersions = new List<EntityModel.DatasetVersion>();
            List<EntityModel.DatasetVersion> insertNewVersions = new List<EntityModel.DatasetVersion>();

            var datasetVersionRepo = _uow.GenericRepository<EntityModel.DatasetVersion>();

            foreach (var newVersion in newVersions)
            {
                Model.DatasetVersion datasetNewVersion = newVersion as Model.DatasetVersion;

                var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                    .GetFirstAsQueryable(_ => _.FundingStreamCode == datasetNewVersion.FundingStream.Id);

                var existingDatasetVersion = datasetVersionRepo.GetFirstAsQueryable(_ => _.DatasetVersionId == datasetNewVersion.Id);

                if (existingDatasetVersion != null)
                {
                    existingDatasetVersion.BlobName = datasetNewVersion.BlobName;
                    existingDatasetVersion.RowCount = datasetNewVersion.RowCount;
                    existingDatasetVersion.NewRowCount = datasetNewVersion.NewRowCount;
                    existingDatasetVersion.AmendedRowCount = datasetNewVersion.AmendedRowCount;
                    existingDatasetVersion.UploadedBlobFilePath = datasetNewVersion.UploadedBlobFilePath;
                    existingDatasetVersion.ChangeType = datasetNewVersion.ChangeType.ToString();
                    existingDatasetVersion.FundingStreamId = fundingStream.FundingStreamId;
                    existingDatasetVersion.ProviderVersionId = datasetNewVersion.ProviderVersionId;
                    existingDatasetVersion.Description = datasetNewVersion.Description;
                    existingDatasetVersion.Date = datasetNewVersion.Date.DateTime;
                    existingDatasetVersion.AuthorId = datasetNewVersion.Author.Id;
                    existingDatasetVersion.AuthorName = datasetNewVersion.Author.Name;
                    existingDatasetVersion.Comment = datasetNewVersion.Comment;
                    existingDatasetVersion.PublishStatus = datasetNewVersion.PublishStatus.ToString();
                    existingDatasetVersion.UpdatedAt = DateTime.Now;

                    updateExistingVersions.Add(existingDatasetVersion);
                }else
                {

                    var datasetOldVersion = datasetVersionRepo.GetFirstAsQueryable(_ => _.DatasetId == datasetNewVersion.DatasetId && _.IsLatest && !_.IsDeleted);

                    if (datasetOldVersion != null)
                    {
                        datasetOldVersion.IsLatest = false;
                        datasetOldVersion.UpdatedAt = DateTime.Now;
                        updateExistingVersions.Add(datasetOldVersion);
                    }

                    var datasetVersion = new EntityModel.DatasetVersion()
                    {
                        DatasetVersionId = datasetNewVersion.Id,
                        DatasetId = datasetNewVersion.DatasetId,
                        BlobName = datasetNewVersion.BlobName,
                        RowCount = datasetNewVersion.RowCount,
                        NewRowCount = datasetNewVersion.NewRowCount,
                        AmendedRowCount = datasetNewVersion.AmendedRowCount,
                        UploadedBlobFilePath = datasetNewVersion.UploadedBlobFilePath,
                        ChangeType = datasetNewVersion.ChangeType.ToString(),
                        FundingStreamId = fundingStream.FundingStreamId,
                        ProviderVersionId = datasetNewVersion.ProviderVersionId,
                        Description = datasetNewVersion.Description,
                        Version = datasetNewVersion.Version,
                        Date = datasetNewVersion.Date.DateTime,
                        AuthorId = datasetNewVersion.Author.Id,
                        AuthorName = datasetNewVersion.Author.Name,
                        Comment = datasetNewVersion.Comment,
                        PublishStatus = datasetNewVersion.PublishStatus.ToString(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsDeleted = false,
                        IsLatest = true
                    };

                    insertNewVersions.Add(datasetVersion);
                }
            }

            if(insertNewVersions.Any()) { datasetVersionRepo.BulkInsertAsync(insertNewVersions); }

            if(updateExistingVersions.Any()) { datasetVersionRepo.BulkUpdate(updateExistingVersions); }

            await _uow.CommitAsync();
        }

        public Task SaveVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism)
        {
            throw new NotImplementedException();
        }
        private Model.DatasetVersion BuildDatasetVersion(EntityModel.DatasetVersion datasetVersion)
        {
            return new Model.DatasetVersion()
            {
                DatasetId = datasetVersion.DatasetId,
                BlobName = datasetVersion.BlobName,
                RowCount = datasetVersion.RowCount,
                NewRowCount = datasetVersion.NewRowCount,
                AmendedRowCount = datasetVersion.AmendedRowCount,
                UploadedBlobFilePath = datasetVersion.UploadedBlobFilePath,
                ChangeType = System.Enum.Parse<Model.DatasetChangeType>(datasetVersion.ChangeType),
                FundingStream = GetFundingStream(datasetVersion.FundingStreamId),
                ProviderVersionId = datasetVersion.ProviderVersionId,
                Description = datasetVersion.Description,
                Version = datasetVersion.Version,
                Date = datasetVersion.Date,
                Author = new Reference()
                {
                    Id = datasetVersion.AuthorId,
                    Name = datasetVersion.AuthorName
                },
                Comment = datasetVersion.Comment,
                PublishStatus = System.Enum.Parse<PublishStatus>(datasetVersion.PublishStatus)
            };
        }

        private Model.DatasetVersion BuildDatasetVersion(EntityModel.DatasetVersion datasetVersion, EntityModel.FundingStream fundingStream)
        {
            return new Model.DatasetVersion()
            {
                DatasetId = datasetVersion.DatasetId,
                BlobName = datasetVersion.BlobName,
                RowCount = datasetVersion.RowCount,
                NewRowCount = datasetVersion.NewRowCount,
                AmendedRowCount = datasetVersion.AmendedRowCount,
                UploadedBlobFilePath = datasetVersion.UploadedBlobFilePath,
                ChangeType = System.Enum.Parse<Model.DatasetChangeType>(datasetVersion.ChangeType),
                FundingStream = new Reference(fundingStream.FundingStreamCode, fundingStream.FundingStreamName),
                ProviderVersionId = datasetVersion.ProviderVersionId,
                Description = datasetVersion.Description,
                Version = datasetVersion.Version,
                Date = datasetVersion.Date,
                Author = new Reference()
                {
                    Id = datasetVersion.AuthorId,
                    Name = datasetVersion.AuthorName
                },
                Comment = datasetVersion.Comment,
                PublishStatus = System.Enum.Parse<PublishStatus>(datasetVersion.PublishStatus)
            };
        }

        private Reference GetFundingStream(int fundingStreamId)
        {
            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>().GetFirstAsQueryable(x => x.FundingStreamId == fundingStreamId);

            return new Reference(fundingStream.FundingStreamCode, fundingStream.FundingStreamName);
        }
    }
}
