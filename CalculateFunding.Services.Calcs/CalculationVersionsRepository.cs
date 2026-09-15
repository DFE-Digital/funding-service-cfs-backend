using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Calcs
{
    public class CalculationVersionsRepository<T> : IVersionRepository<T> where T : VersionedItem
    {
        protected readonly IUnitOfWork _uow;

        public CalculationVersionsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(CalculationsRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }


        public async Task<T> CreateVersion(T newVersion, T currentVersion = default, string partitionKey = null, bool incrementFromCurrentVersion = false)
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

        public Task<IEnumerable<T>> GetAllVersions()
        {
            throw new NotImplementedException();
        }

        public async Task<int> GetNextVersionNumber(T version = default, int currentVersion = 0, string partitionKeyId = null, bool incrementFromCurrentVersion = false)
        {
            Guard.ArgumentNotNull(version, nameof(version));

            if (incrementFromCurrentVersion)
            {
                return currentVersion + 1;
            }

            var maxVersion = _uow.GenericRepository<EntityModel.CalculationVersion>()
                .GetFirstAsQueryable(_ => _.CalculationId == version.EntityId && _.IsLatest && !_.IsDeleted).Version;

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

        public async Task<IEnumerable<T>> GetVersions(string entityId, string partitionKeyId = null)
        {
            Guard.ArgumentNotNull(entityId, nameof(entityId));

            var calculationVersonRepo = _uow.GenericRepository<EntityModel.CalculationVersion>();

            var calculationVersions = await calculationVersonRepo.GetManyAsQueryable(_ => _.CalculationId == entityId && !_.IsDeleted).ToListAsync();

            if (!calculationVersions.Any()) { return Enumerable.Empty<T>(); }

            return (IEnumerable<T>)calculationVersions.Select(_ => BuildCalculationVersion(_));
        }

        public Task<IEnumerable<T>> GetVersions(string entityId, int? offset, int? limit)
        {
            throw new NotImplementedException();
        }
        public async Task<HttpStatusCode> SaveVersion(T newVersion)
        {
            Guard.ArgumentNotNull(newVersion, nameof(newVersion));

            CalculationVersion calculationNewVersion = newVersion as CalculationVersion;

            var calculationVersonRepo = _uow.GenericRepository<EntityModel.CalculationVersion>();

            var exisitngCalculationVersion = calculationVersonRepo.GetFirstAsQueryable(_ => _.CalculationId == calculationNewVersion.CalculationId && _.IsLatest && !_.IsDeleted);

            if (exisitngCalculationVersion != null)
            {
                exisitngCalculationVersion.IsLatest = false;
                exisitngCalculationVersion.UpdatedAt = DateTime.Now;
                calculationVersonRepo.Update(exisitngCalculationVersion);
            }

            EntityModel.CalculationVersion calculationVersion = new EntityModel.CalculationVersion()
            {
                CalculationVersionId = calculationNewVersion.Id,
                CalculationId = calculationNewVersion.CalculationId,
                SourceCode = calculationNewVersion.SourceCode,
                CalculationType = calculationNewVersion.CalculationType.ToString(),
                SourceCodeName = calculationNewVersion.SourceCodeName,
                Name = calculationNewVersion.Name,
                Namespace = calculationNewVersion.Namespace.ToString(),
                WasTemplateCalculation = calculationNewVersion.WasTemplateCalculation,
                ValueType = calculationNewVersion.ValueType.ToString(),
                Description = calculationNewVersion.Description,
                DataType = calculationNewVersion.DataType.ToString(),
                AllowedTypeValues = (calculationNewVersion.AllowedEnumTypeValues != null && calculationNewVersion.AllowedEnumTypeValues.Any()) 
                ? JsonConvert.SerializeObject(calculationNewVersion.AllowedEnumTypeValues) : null,
                Version = calculationNewVersion.Version,
                Date = DateTime.Now,
                AuthorId = calculationNewVersion.Author.Id,
                AuthorName = calculationNewVersion.Author.Name,
                Comment = calculationNewVersion.Comment,
                PublishStatus = calculationNewVersion.PublishStatus.ToString(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false,
                IsLatest = true
            };

            calculationVersonRepo.Insert(calculationVersion);
            await _uow.CommitAsync();
            return HttpStatusCode.OK;
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

        private CalculationVersion BuildCalculationVersion(EntityModel.CalculationVersion calculationVersion)
        {
            return new CalculationVersion()
            {
                CalculationId = calculationVersion.CalculationId,
                SourceCode = calculationVersion.SourceCode,
                CalculationType = Enum.Parse<CalculationType>(calculationVersion.CalculationType),
                SourceCodeName = calculationVersion.SourceCodeName,
                Name = calculationVersion?.Name,
                Date = calculationVersion.Date,
                Version = calculationVersion.Version,
                PublishStatus = Enum.Parse<PublishStatus>(calculationVersion.PublishStatus),
                Namespace = Enum.Parse<CalculationNamespace>(calculationVersion.Namespace),
                WasTemplateCalculation = calculationVersion.WasTemplateCalculation,
                ValueType = Enum.Parse<CalculationValueType>(calculationVersion.ValueType),
                Description = calculationVersion.Description,
                Author = new Reference(calculationVersion.AuthorId, calculationVersion.AuthorName),
                DataType = calculationVersion?.DataType != null ? Enum.Parse<CalculationDataType>(calculationVersion?.DataType) : CalculationDataType.Decimal, //bydefault CalculationDataType.Decimal
                AllowedEnumTypeValues = calculationVersion.AllowedTypeValues != null
                ? JsonConvert.DeserializeObject<IEnumerable<string>>(calculationVersion.AllowedTypeValues) 
                : null
            }; 
        }
    }
}
