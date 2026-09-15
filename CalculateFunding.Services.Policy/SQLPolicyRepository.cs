using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Policy;
using CalculateFunding.Models.Policy.FundingPolicy;
using CalculateFunding.Services.Policy.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using EnumerableAccessor = System.Linq.Enumerable;

namespace CalculateFunding.Services.Policy
{
    public class SQLPolicyRepository : IPolicyRepository, IHealthChecker
    {
        Common.EfCore.UnitOfWork.IUnitOfWork _uow;
        public SQLPolicyRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(SQLPolicyRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public async Task<FundingConfiguration> GetFundingConfiguration(string configId)
        {
            Guard.IsNullOrWhiteSpace(configId, nameof(configId));

            var fundingConfiguration = _uow.GenericRepository<EntityModel.Policy>()
                .GetFirstAsQueryable(_ => _.PolicyId == configId && _.IsDeleted == false);

            return (fundingConfiguration != null) 
                ? JsonConvert.DeserializeObject<FundingConfiguration>(fundingConfiguration?.Content)
                : null;

        }

        public async Task<IEnumerable<FundingConfiguration>> GetFundingConfigurations()
        {
            var fundingConfigurations = _uow.GenericRepository<EntityModel.Policy>().GetManyAsQueryable(_=> _.IsDeleted == false);

            return fundingConfigurations.Select(_ => JsonConvert.DeserializeObject<FundingConfiguration>(_.Content)).ToList();
        }

        public async Task<IEnumerable<FundingConfiguration>> GetFundingConfigurationsByFundingStreamId(string fundingStreamId)
        {
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                                .GetFirstAsQueryable(_ => _.FundingStreamCode == fundingStreamId);
            var fundingConfigs = _uow.GenericRepository<EntityModel.Policy>().GetManyAsQueryable(_=>_.FundingStreamId == fundingStream.FundingStreamId);

            IEnumerable<string> fundingConfigurations = fundingConfigs.Select(_ => _.Content);

            return fundingConfigurations.Any()
                ? fundingConfigurations.Select(_ => JsonConvert.DeserializeObject<FundingConfiguration>(_)).ToList()
                : Enumerable.Empty<FundingConfiguration>();

        }

        public async Task<FundingDate> GetFundingDate(string fundingDateId)
        {
            Guard.IsNullOrWhiteSpace(fundingDateId, nameof(fundingDateId));

            var fundingData = _uow.GenericRepository<EntityModel.Policy>()
                .GetFirstAsQueryable(_ => _.PolicyId == fundingDateId && _.IsDeleted == false);

            return fundingData != null
                ? JsonConvert.DeserializeObject<FundingDate>(fundingData?.Content)
                : null;
        }

        public async Task<FundingPeriod> GetFundingPeriodById(string fundingPeriodId)
        {
            Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));

            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetFirstAsQueryable(_ => _.FundingPeriodCode == fundingPeriodId);

            return (fundingPeriod != null) 
                    ? new FundingPeriod()
                        {
                            Id = fundingPeriodId,
                            EndDate = (DateTimeOffset)fundingPeriod.EndDate,
                            Name = fundingPeriod.FundingPeriodName,
                            Period = fundingPeriod.Period,
                            StartDate = (DateTimeOffset)fundingPeriod.StartDate,
                            Type = (FundingPeriodType)Enum.Parse(typeof(FundingPeriodType), fundingPeriod.Type),

                        } 
                     :  null ;
        }

        public async Task<IEnumerable<FundingPeriod>> GetFundingPeriods(Expression<Func<DocumentEntity<FundingPeriod>, bool>> query = null)
        {
            var fundingPeriods = _uow.GenericRepository<EntityModel.FundingPeriod>().Get();

            return fundingPeriods.Select(fp => new FundingPeriod()
            {
                Id = fp.FundingPeriodCode,
                EndDate = (DateTimeOffset)fp.EndDate,
                Name = fp.FundingPeriodName,
                Period = fp.Period,
                StartDate = (DateTimeOffset)fp.StartDate,
                Type = (FundingPeriodType)Enum.Parse(typeof(FundingPeriodType), fp.Type),

            });
        }

        public async Task<FundingStream> GetFundingStreamById(string fundingStreamId)
        {
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirstAsQueryable(_ => _.FundingStreamCode == fundingStreamId);

            return (fundingStream != null) 
                ? new FundingStream()
                {
                    Id = fundingStreamId,
                    Name = fundingStream.FundingStreamName,
                    ShortName = fundingStream.FundingStreamCode
                }
                : null;
        }

        public async Task<IEnumerable<FundingStream>> GetFundingStreams(Expression<Func<DocumentEntity<FundingStream>, bool>> query = null)
        {
            var fundingStreams = _uow.GenericRepository<EntityModel.FundingStream>().Get();

            return fundingStreams.Select(fs => new FundingStream()
            {
                ShortName = fs.ShortFundingStreamName,
                Id = fs.FundingStreamCode,
                Name = fs.FundingStreamName,
            });
        }

        public async Task<HttpStatusCode> SaveFundingConfiguration(FundingConfiguration configuration)
        {
            Guard.ArgumentNotNull(configuration, nameof(configuration));
            var fundingConfiguration = _uow.GenericRepository<EntityModel.Policy>()
                .GetFirstAsQueryable(_ => _.PolicyId == configuration.Id && _.IsDeleted == false);

            var repo = _uow.GenericRepository<EntityModel.Policy>();

            if (fundingConfiguration == null)
            {
                var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                    .GetFirstAsQueryable(_ => _.FundingStreamCode == configuration.FundingStreamId);

                var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                    .GetFirstAsQueryable(_ => _.FundingPeriodCode == configuration.FundingPeriodId);

                fundingConfiguration = new EntityModel.Policy()
                {
                    PolicyId = configuration.Id,
                    DocumentType = nameof(FundingConfiguration),
                    Content = JsonConvert.SerializeObject(configuration),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    FundingStreamId = fundingStream.FundingStreamId,
                    FundingPeriodId = fundingPeriod.FundingPeriodId

                };
                repo.Insert(fundingConfiguration);
            }
            else
            {
                fundingConfiguration.Content = JsonConvert.SerializeObject(configuration);
                fundingConfiguration.UpdatedAt = DateTime.Now;
                repo.Update(fundingConfiguration);
            }

            await _uow.CommitAsync();

            return HttpStatusCode.Created;
        }

        public async Task<HttpStatusCode> SaveFundingDate(FundingDate fundingDate)
        {
            Guard.ArgumentNotNull(fundingDate, nameof(fundingDate));
            var existingFundingData = _uow.GenericRepository<EntityModel.Policy>()
                .GetFirstAsQueryable(_ => _.PolicyId == fundingDate.Id && _.IsDeleted == false);

            var repo = _uow.GenericRepository<EntityModel.Policy>();

            if (existingFundingData == null)
            {
                var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                    .GetFirstAsQueryable(_ => _.FundingStreamCode == fundingDate.FundingStreamId);

                var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                    .GetFirstAsQueryable(_ => _.FundingPeriodCode == fundingDate.FundingPeriodId);

                existingFundingData = new EntityModel.Policy()
                {
                    PolicyId = fundingDate.Id,
                    DocumentType = nameof(FundingDate),
                    Content = JsonConvert.SerializeObject(fundingDate),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    FundingStreamId = fundingStream.FundingStreamId,
                    FundingPeriodId = fundingPeriod.FundingPeriodId

                };
                repo.Insert(existingFundingData);
            }
            else
            {
                existingFundingData.Content = JsonConvert.SerializeObject(fundingDate);
                existingFundingData.UpdatedAt = DateTime.Now;
                repo.Update(existingFundingData);
            }

            await _uow.CommitAsync();

            return HttpStatusCode.Created;
        }

        public async Task SaveFundingPeriods(IEnumerable<FundingPeriod> fundingPeriods)
        {
            Guard.ArgumentNotNull(fundingPeriods, nameof(fundingPeriods));
            var repo = _uow.GenericRepository<EntityModel.FundingPeriod>();

            fundingPeriods = EnumerableAccessor.DistinctBy(fundingPeriods, _ => _.Id);
            
            foreach (var fundingPeriod in  fundingPeriods)
            {
                var existingFundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                     .GetFirstAsQueryable(_ => _.FundingPeriodCode == fundingPeriod.Id);

                if (existingFundingPeriod == null)
                {
                    existingFundingPeriod = new EntityModel.FundingPeriod()
                    {
                        FundingPeriodCode = fundingPeriod.Id,
                        FundingPeriodName = fundingPeriod.Name,
                        StartDate = fundingPeriod.StartDate.DateTime,
                        EndDate = fundingPeriod.EndDate.DateTime,
                        EndYear = fundingPeriod.EndDate.Year,
                        StartYear = fundingPeriod.StartDate.Year,
                        Type = fundingPeriod.Type.ToString(),
                        Period = fundingPeriod.Period
                    };
                    repo.Insert(existingFundingPeriod);
                }
                else
                {
                    existingFundingPeriod.FundingPeriodName = fundingPeriod.Name;
                    existingFundingPeriod.StartDate = fundingPeriod.StartDate.DateTime;
                    existingFundingPeriod.EndDate = fundingPeriod.EndDate.DateTime;
                    existingFundingPeriod.EndYear = fundingPeriod.EndDate.Year;
                    existingFundingPeriod.StartYear = fundingPeriod.StartDate.Year;
                    existingFundingPeriod.Type = fundingPeriod.Type.ToString();
                    existingFundingPeriod.Period = fundingPeriod.Period;

                    repo.Update(existingFundingPeriod);
                }
            }

            await _uow.CommitAsync();
        }

        public async Task<HttpStatusCode> SaveFundingStream(FundingStream fundingStream)
        {
            Guard.ArgumentNotNull(fundingStream, nameof(fundingStream));
            var existingFundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirstAsQueryable(_ => _.FundingStreamCode == fundingStream.Id);

            var repo = _uow.GenericRepository<EntityModel.FundingStream>();
            if (existingFundingStream == null)
            {
                existingFundingStream = new EntityModel.FundingStream()
                {
                    FundingStreamCode = fundingStream.Id,
                    FundingStreamName = fundingStream.Name,
                    ShortFundingStreamName = fundingStream.ShortName
                };

                repo.Insert(existingFundingStream);
            } else
            {
                existingFundingStream.FundingStreamName = fundingStream.Name;
                existingFundingStream.ShortFundingStreamName = fundingStream.ShortName;
                repo.Update(existingFundingStream);
            }  
            await _uow.CommitAsync();

            return HttpStatusCode.Created;
        }
    }
}
