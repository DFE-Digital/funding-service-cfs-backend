using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Providers;
using CalculateFunding.Services.Providers.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Providers
{
    public class ProviderVersionsMetadataRepository : IProviderVersionsMetadataRepository, IHealthChecker
    {
        private const string MASTER_KEY = "master";
        Common.EfCore.UnitOfWork.IUnitOfWork _uow;
        public ProviderVersionsMetadataRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public async Task<HttpStatusCode> CreateProviderVersion(ProviderVersionMetadata providerVersion)
        {
            Guard.ArgumentNotNull(providerVersion, nameof(providerVersion));
            var repo = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>();

            var fundingStreamId = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamCode == providerVersion.FundingStream);

            var existingproviderVersion = new EntityModel.ProviderVersionMetadatum()
            {
                ProviderVersionMetadataId = ((ProviderVersion)providerVersion).Id,
                ProviderVersionId = providerVersion.ProviderVersionId,
                VersionType = providerVersion.VersionType.ToString(),
                Name = providerVersion.Name,
                Description = providerVersion.Description,
                Version = providerVersion.Version,
                TargetDate = providerVersion.TargetDate.DateTime,
                FundingStreamId = fundingStreamId.FundingStreamId,
                ValidationResult = providerVersion.ValidationResult,
                Created = providerVersion.Created.DateTime,
                CreatedAt = DateTime.Now,
                IsDeleted = false,
            };

            await repo.Upsert(existingproviderVersion, e => e.ProviderVersionMetadataId == ((ProviderVersion)providerVersion).Id);
            await _uow.CommitAsync();

            return HttpStatusCode.OK;

        }

        public async Task<bool> Exists(string name, string providerVersionTypeString, int version, string fundingStream)
        {
            var fundingStreamId = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStream);

            var providerVersions = await _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>().
                GetManyAsQueryable(_ => _.VersionType == providerVersionTypeString && _.Version == version 
                && _.Name.ToLower() == name.ToLower()
                && _.FundingStreamId == fundingStreamId.FundingStreamId && !_.IsDeleted).ToListAsync();

            return providerVersions.Any();

        }

        public async Task<IEnumerable<CurrentProviderVersion>> GetAllCurrentProviderVersions()
        {
            var currentProviderVersions = await _uow.GenericRepository<EntityModel.CurrentProviderVersion>()
                .GetAllAsyn();
            var providerVersionFundingPeriods = await _uow.GenericRepository<EntityModel.ProviderVersionFundingPeriodDetail>()
                .GetManyAsQueryable(_ => currentProviderVersions.Select(s => s.CurrentProviderVersionId).Contains(_.CurrentProviderVersionId)).ToListAsync();

            var fundingPeriods = await GetfundingPeriods(providerVersionFundingPeriods);
            var result = currentProviderVersions.Select(c => new CurrentProviderVersion
            {
                Id = c.CurrentProviderVersionId,
                ProviderVersionId = c.ProviderVersionId,
                ProviderSnapshotId = c.ProviderSnapshotId,
                FundingPeriod = [.. providerVersionFundingPeriods.Where(x => x.CurrentProviderVersionId == c.CurrentProviderVersionId)
                                .Select(p => new ProviderSnapShotByFundingPeriod
                                {
                                    ProviderSnapshotId = p.ProviderSnapshotId,
                                    ProviderVersionId = p.ProviderVersionId,
                                    FundingPeriodName = fundingPeriods.SingleOrDefault(r => r.FundingPeriodId == p.FundingPeriodId).FundingPeriodCode,
                                })]

            });
    
            return result;

        }

        public async Task<CurrentProviderVersion> GetCurrentProviderVersion(string fundingStreamId)
        {
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
            if (AdultStream.IsExists(fundingStreamId))
            {
                fundingStreamId = AdultStream.GetParent();
            }

            var id= $"Current_{fundingStreamId}";
            var currentProviderVersion = await _uow.GenericRepository<EntityModel.CurrentProviderVersion>().FirstOrDefaultAsync(_ => _.CurrentProviderVersionId == id && !_.IsDeleted);

            if (currentProviderVersion == null) { return null; }
         
            var providerVersionFundingPeriods = await _uow.GenericRepository<EntityModel.ProviderVersionFundingPeriodDetail>().GetManyAsQueryable(_ => _.CurrentProviderVersionId == id).ToListAsync();

            var fundingPeriods = await GetfundingPeriods(providerVersionFundingPeriods);

            return new CurrentProviderVersion()
            {
                Id = currentProviderVersion.CurrentProviderVersionId,
                ProviderVersionId = currentProviderVersion.ProviderVersionId,
                ProviderSnapshotId = currentProviderVersion.ProviderSnapshotId,
                FundingPeriod = [.. providerVersionFundingPeriods.Where(x => x.CurrentProviderVersionId == currentProviderVersion.CurrentProviderVersionId)
                                .Select(p => new ProviderSnapShotByFundingPeriod
                                {
                                    ProviderSnapshotId = p.ProviderSnapshotId,
                                    ProviderVersionId = p.ProviderVersionId,
                                    FundingPeriodName = fundingPeriods.SingleOrDefault(r => r.FundingPeriodId == p.FundingPeriodId).FundingPeriodCode,
                                })]
            };

        }

        private async Task<List<EntityModel.FundingPeriod>> GetfundingPeriods(List<EntityModel.ProviderVersionFundingPeriodDetail> providerVersionFundingPeriods)
        {
            return await _uow.GenericRepository<EntityModel.FundingPeriod>().GetManyAsQueryable(_ => providerVersionFundingPeriods.
                                                Select(s => s.FundingPeriodId).Contains(_.FundingPeriodId)).ToListAsync();
        }

        public async Task<MasterProviderVersion> GetMasterProviderVersion()
        {
            var providerVersion = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>().GetFirstAsQueryable(_ => _.ProviderVersionMetadataId == MASTER_KEY && !_.IsDeleted);

            if (providerVersion == null) { return null; }

            var fundingStreamCode = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamId == providerVersion.FundingStreamId);

            return new MasterProviderVersion()
            {
                Id = MASTER_KEY,
                ProviderVersionId = providerVersion.ProviderVersionId,
                ProviderVersionTypeString = providerVersion.VersionType,
                ValidationResult = providerVersion.ValidationResult,
                VersionType = Enum.Parse<ProviderVersionType>(providerVersion.VersionType),
                Name = providerVersion.Name,
                Description = providerVersion.Description,
                Version = providerVersion.Version,
                TargetDate = providerVersion.TargetDate,
                FundingStream = fundingStreamCode.FundingStreamCode,
                Created = providerVersion.Created,
            };
        }

        public async Task<ProviderVersionByDate> GetProviderVersionByDate(int year, int month, int day)
        {
            var id = $"{year}{month:00}{day:00}";

            var providerVersion = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>().GetFirstAsQueryable(_ => _.ProviderVersionMetadataId == id && !_.IsDeleted);

            if (providerVersion == null) { return null; }

            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamId == providerVersion.FundingStreamId);

            return new ProviderVersionByDate()
            {
                Id = id,
                Day = day,
                Month = month,
                Year = year,
                ProviderVersionId = providerVersion.ProviderVersionId,
                ProviderVersionTypeString = providerVersion.VersionType,
                ValidationResult = providerVersion.ValidationResult,
                VersionType = Enum.Parse<ProviderVersionType>(providerVersion.VersionType),
                Name = providerVersion.Name,
                Description = providerVersion.Description,
                Version = providerVersion.Version,
                TargetDate = providerVersion.TargetDate,
                FundingStream = fundingStream.FundingStreamCode,
                Created = providerVersion.Created,
            };
        }

        public async Task<ProviderVersionMetadata> GetProviderVersionMetadata(string providerVersionId)
        {
            Guard.IsNullOrWhiteSpace(providerVersionId, nameof(providerVersionId));

            string key = $"providerVersion-{providerVersionId}";

            var providerVersionRepo = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();

            var providerVersion = await providerVersionRepo
                .SingleOrDefaultAsync(_ => _.ProviderVersionMetadataId == key && !_.IsDeleted);

            if (providerVersion == null) 
            { 
                return null; 
            }

            var fundingStreamCode = await fundingStreamRepo
                .SingleOrDefaultAsync(_ => _.FundingStreamId == providerVersion.FundingStreamId);

            return new ProviderVersionMetadata()
            {
                ProviderVersionId = providerVersion.ProviderVersionId,
                ProviderVersionTypeString = providerVersion.VersionType,
                ValidationResult = providerVersion.ValidationResult,
                VersionType = Enum.Parse<ProviderVersionType>(providerVersion.VersionType),
                Name = providerVersion.Name,
                Description = providerVersion.Description,
                Version = providerVersion.Version,
                TargetDate = providerVersion.TargetDate,
                FundingStream = fundingStreamCode.FundingStreamCode,
                Created = providerVersion.Created,
            };
        }

        public async Task<IEnumerable<ProviderVersionMetadata>> GetProviderVersions(string fundingStream)
        {
            Guard.IsNullOrWhiteSpace(fundingStream, nameof(fundingStream));
            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStream);

            var providerVersions = await _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>().GetManyAsQueryable(_ => _.FundingStreamId == fundingStreams.FundingStreamId && !_.IsDeleted)
                .Select(t => new ProviderVersionMetadata
                {
                    ProviderVersionId = t.ProviderVersionId,
                    ProviderVersionTypeString = t.VersionType,
                    ValidationResult = t.ValidationResult,
                    VersionType = Enum.Parse<ProviderVersionType>(t.VersionType),
                    Name = t.Name,
                    Description = t.Description,
                    Version = t.Version,
                    TargetDate = t.TargetDate,
                    FundingStream = fundingStreams.FundingStreamCode,
                    Created = t.Created,
                }).ToListAsync();

            return providerVersions;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProviderVersionsMetadataRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public async Task<HttpStatusCode> UpsertCurrentProviderVersion(CurrentProviderVersion currentProviderVersion)
        {
            var repoProviderVersion = _uow.GenericRepository<EntityModel.CurrentProviderVersion>();
            var repoFundingPeriodDetail = _uow.GenericRepository<EntityModel.ProviderVersionFundingPeriodDetail>();

            var existingProviderVersion = new EntityModel.CurrentProviderVersion()
            {
                CurrentProviderVersionId = currentProviderVersion.Id,
                ProviderSnapshotId = currentProviderVersion.ProviderSnapshotId,
                ProviderVersionId = currentProviderVersion.ProviderVersionId,
                IsDeleted = false,
            };

            await repoProviderVersion.Upsert(existingProviderVersion, _ =>_.CurrentProviderVersionId == currentProviderVersion.Id);

            if (currentProviderVersion.FundingPeriod.Count != 0)
            {
                foreach (var fundingPeriod in currentProviderVersion.FundingPeriod)
                {
                    var fundingPeriodId = await GetFundingPeriodId(fundingPeriod.FundingPeriodName);

                    var existingFundingPeriodDetail = new EntityModel.ProviderVersionFundingPeriodDetail
                    {
                        CurrentProviderVersionId = currentProviderVersion.Id,
                        FundingPeriodId = fundingPeriodId,
                        ProviderVersionId = fundingPeriod.ProviderVersionId,
                        ProviderSnapshotId = Convert.ToInt32(fundingPeriod.ProviderSnapshotId),
                    };

                    await repoFundingPeriodDetail.Upsert(existingFundingPeriodDetail, e => e.CurrentProviderVersionId == currentProviderVersion.Id && e.FundingPeriodId == existingFundingPeriodDetail.FundingPeriodId);
                }
            }
            await _uow.CommitAsync();
            return HttpStatusCode.Created;

        }

        private async Task<int> GetFundingPeriodId(string fundingPeriodName)
        {
             var fundingPeriod = await _uow.GenericRepository<EntityModel.FundingPeriod>().SingleOrDefaultAsync(_ => _.FundingPeriodCode == fundingPeriodName);
             return fundingPeriod.FundingPeriodId;
        }

        public async Task<HttpStatusCode> UpsertMaster(MasterProviderVersion masterProviderVersion)
        {
            Guard.ArgumentNotNull(masterProviderVersion, nameof(masterProviderVersion));

            var repo = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>();

            var existingproviderVersion = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>()
                .GetFirstAsQueryable(_ => _.ProviderVersionMetadataId == MASTER_KEY);

            var fundingStreamId = await GetFundingPeriodId(masterProviderVersion.FundingStream);

            existingproviderVersion = new EntityModel.ProviderVersionMetadatum()
            {
                ProviderVersionMetadataId = MASTER_KEY,
                ProviderVersionId = masterProviderVersion.ProviderVersionId,
                VersionType = masterProviderVersion.VersionType.ToString(),
                Name = masterProviderVersion.Name,
                Description = masterProviderVersion.Description,
                Version = masterProviderVersion.Version,
                TargetDate = masterProviderVersion.TargetDate.DateTime,
                FundingStreamId = fundingStreamId,
                ValidationResult = masterProviderVersion.ValidationResult,
                Created = masterProviderVersion.Created.DateTime,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false,
            };

            await repo.Upsert(existingproviderVersion, e => e.ProviderVersionMetadataId == MASTER_KEY);
            await _uow.CommitAsync();
            return HttpStatusCode.OK;
        }
        public async Task<HttpStatusCode> UpsertProviderVersionByDate(ProviderVersionByDate providerVersionByDate)
        {     
            Guard.ArgumentNotNull(providerVersionByDate, nameof(providerVersionByDate));

            providerVersionByDate.Id = $"{providerVersionByDate.Year}{providerVersionByDate.Month:00}{providerVersionByDate.Day:00}";

            var repo = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>();

            var existingproviderVersion = _uow.GenericRepository<EntityModel.ProviderVersionMetadatum>()
                .GetFirstAsQueryable(_ => _.ProviderVersionMetadataId == providerVersionByDate.Id);

            var fundingStreamId = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamCode == providerVersionByDate.FundingStream);

            if (existingproviderVersion == null)
            {
                existingproviderVersion = new EntityModel.ProviderVersionMetadatum()
                {
                    ProviderVersionMetadataId = providerVersionByDate.Id,
                    ProviderVersionId = providerVersionByDate.ProviderVersionId,
                    VersionType = providerVersionByDate.VersionType.ToString(),
                    Name = providerVersionByDate.Name,
                    Description = providerVersionByDate.Description,
                    Version = providerVersionByDate.Version,
                    TargetDate = providerVersionByDate.TargetDate.DateTime,
                    FundingStreamId = fundingStreamId.FundingStreamId,
                    ValidationResult = providerVersionByDate.ValidationResult,
                    Created = providerVersionByDate.Created.DateTime,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false,
                };

                repo.Insert(existingproviderVersion);
            }
            else
            {
                existingproviderVersion.ProviderVersionId = providerVersionByDate.ProviderVersionId;
                existingproviderVersion.VersionType = providerVersionByDate.VersionType.ToString();
                existingproviderVersion.Name = providerVersionByDate.Name;
                existingproviderVersion.Description = providerVersionByDate.Description;
                existingproviderVersion.Version = providerVersionByDate.Version;
                existingproviderVersion.TargetDate = providerVersionByDate.TargetDate.DateTime;
                existingproviderVersion.FundingStreamId = fundingStreamId.FundingStreamId;
                existingproviderVersion.ValidationResult = providerVersionByDate.ValidationResult;
                existingproviderVersion.UpdatedAt = DateTime.Now;
                existingproviderVersion.IsDeleted = false;
                repo.Update(existingproviderVersion);
            }
            await _uow.CommitAsync();

            return HttpStatusCode.OK;
        }
    }
}
