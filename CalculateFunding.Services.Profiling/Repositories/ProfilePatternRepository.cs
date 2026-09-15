using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Profiling.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Profiling.Repositories
{
    public class ProfilePatternRepository : IProfilePatternRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;
        public ProfilePatternRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProfilePatternRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public async Task<HttpStatusCode> DeleteProfilePattern(string id)
        {
            Guard.ArgumentNotNull(id, nameof(id));

            var profileRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();

            var profilePattern = await profileRepo.FirstOrDefaultAsync(_ => _.Id == id && !_.IsDeleted);

            if (profilePattern == null)
            {
                return HttpStatusCode.NotFound;
            }

            profilePattern.IsDeleted = true;
            profilePattern.UpdatedAt = DateTime.Now;

            profileRepo.Update(profilePattern);

            await _uow.CommitAsync();

            return HttpStatusCode.NoContent;
        }

        public async Task<FundingStreamPeriodProfilePattern> GetProfilePattern(string fundingPeriodId, string fundingStreamId, string fundingLineCode, string profilePatternKey)
        {
            Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
            Guard.IsNullOrWhiteSpace(fundingLineCode, nameof(fundingLineCode));

            var fundingPeriodRepo = _uow.GenericRepository<EntityModel.FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();
            var profilePatternRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();
            var profilePatternPeriodRepo = _uow.GenericRepository<EntityModel.ProfilePatternPeriod>();
            var providerTypeSubTypesRepo = _uow.GenericRepository<EntityModel.ProviderTypeSubType>();
            var reProfilingConfigurationPropertyRepo = _uow.GenericRepository<EntityModel.ReProfilingConfigurationProperty>();

            var (fundingPeriod, fundingStream) = await GetFundingPeriodAndStream(
                () => fundingPeriodRepo.SingleOrDefaultAsync(_ => _.FundingPeriodCode == fundingPeriodId),
                () => fundingStreamRepo.SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStreamId));

            if (fundingPeriod == null || fundingStream == null)
            {
                return null;
            }

            EntityModel.FundingStreamPeriodProfilePattern profilePatternEntity;

            if (string.IsNullOrWhiteSpace(profilePatternKey))
            {
                profilePatternEntity = profilePatternRepo.GetFirstAsQueryable(_ =>
                    _.FundingPeriodId == fundingPeriod.FundingPeriodId
                    && _.FundingStreamId == fundingStream.FundingStreamId
                    && _.FundingLineId == fundingLineCode
                    && !_.IsDeleted
                    && (string.IsNullOrEmpty(_.ProfilePatternKey)));
            }
            else
            {
                profilePatternEntity = profilePatternRepo.GetFirstAsQueryable(_ =>
                    _.FundingPeriodId == fundingPeriod.FundingPeriodId
                    && _.FundingStreamId == fundingStream.FundingStreamId
                    && _.FundingLineId == fundingLineCode
                    && !_.IsDeleted
                    && _.ProfilePatternKey == profilePatternKey);
            }

            if (profilePatternEntity == null)
            {
                return null;
            }

            return await MapProfilePattern(profilePatternEntity, fundingPeriod, fundingStream);
        }

        public async Task<FundingStreamPeriodProfilePattern> GetProfilePattern(string id)
        {
            Guard.IsNullOrWhiteSpace(id, nameof(id));

            var fundingPeriodRepo = _uow.GenericRepository<EntityModel.FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();
            var profilePatternRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();
            var profilePatternPeriodRepo = _uow.GenericRepository<EntityModel.ProfilePatternPeriod>();
            var providerTypeSubTypesRepo = _uow.GenericRepository<EntityModel.ProviderTypeSubType>();
            var reProfilingConfigurationPropertyRepo = _uow.GenericRepository<EntityModel.ReProfilingConfigurationProperty>();

            var profilePatternEntity = profilePatternRepo.GetFirstorDefault(_ => _.Id == id && !_.IsDeleted);

            if (profilePatternEntity == null)
            {
                return null;
            }

            var (fundingPeriod, fundingStream) = await GetFundingPeriodAndStream(
                () => fundingPeriodRepo.SingleOrDefaultAsync(_ => _.FundingPeriodId == profilePatternEntity.FundingPeriodId),
                () => fundingStreamRepo.SingleOrDefaultAsync(_ => _.FundingStreamId == profilePatternEntity.FundingStreamId));

            if (fundingPeriod == null || fundingStream == null)
            {
                return null;
            }

            return await MapProfilePattern(profilePatternEntity, fundingPeriod, fundingStream);
        }

        private async Task<FundingStreamPeriodProfilePattern> MapProfilePattern(EntityModel.FundingStreamPeriodProfilePattern profilePatternEntity,
            EntityModel.FundingPeriod fundingPeriod,
            EntityModel.FundingStream fundingStream)
        {
            var profilePatternPeriodRepo = _uow.GenericRepository<EntityModel.ProfilePatternPeriod>();
            var providerTypeSubTypesRepo = _uow.GenericRepository<EntityModel.ProviderTypeSubType>();
            var reProfilingConfigurationPropertyRepo = _uow.GenericRepository<EntityModel.ReProfilingConfigurationProperty>();

            var profilePatternPeriods = await profilePatternPeriodRepo
                .GetManyAsQueryable(_ => _.FundingStreamPeriodProfilePatternId == profilePatternEntity.Id)
                .ToListAsync();

            var providerTypeSubTypes = await providerTypeSubTypesRepo
                .GetManyAsQueryable(_ => _.FundingStreamPeriodProfilePatternId == profilePatternEntity.Id)
                .ToListAsync();

            var reProfilingConfigurationProperties = await reProfilingConfigurationPropertyRepo
                .GetManyAsQueryable(_ => _.FundingStreamPeriodProfilePatternId == profilePatternEntity.Id)
                .ToListAsync();

            ProfilePatternReProfilingConfiguration reProfilingConfiguration = BuildReProfilingConfiguration(reProfilingConfigurationProperties);

            return new FundingStreamPeriodProfilePattern
            {
                FundingPeriodId = fundingPeriod.FundingPeriodCode,
                FundingStreamId = fundingStream.FundingStreamCode,
                FundingLineId = profilePatternEntity.FundingLineId,
                ProfilePatternKey = profilePatternEntity.ProfilePatternKey,
                ProfilePatternType = string.IsNullOrWhiteSpace(profilePatternEntity.ProfilePatternType)
                    ? (ProfilePatternType?)null
                    : Enum.Parse<ProfilePatternType>(profilePatternEntity.ProfilePatternType, true),
                RoundingStrategy = Enum.Parse<RoundingStrategy>(profilePatternEntity.RoundingStrategy, true),
                FundingStreamPeriodStartDate = profilePatternEntity.FundingStreamPeriodStartDate,
                FundingStreamPeriodEndDate = profilePatternEntity.FundingStreamPeriodEndDate,
                AllowUserToEditProfilePattern = profilePatternEntity.AllowUserToEditProfilePattern,
                ProfilePatternDisplayName = profilePatternEntity.ProfilePatternDisplayName,
                ProfilePatternDescription = profilePatternEntity.ProfilePatternDescription,
                ProfileCacheETag = profilePatternEntity.ProfileCacheEtag,
                ProfilePatternOpenDateConfiguration = string.IsNullOrWhiteSpace(profilePatternEntity.ProfilePatternOpenDateConfig)
                    ? null
                    : JsonConvert.DeserializeObject<IEnumerable<ProfilePatternOpenDateConfiguration>>(profilePatternEntity.ProfilePatternOpenDateConfig),
                ProfilePattern = profilePatternPeriods
                    .OrderBy(_ => _.PeriodYear)
                    .ThenBy(_ => _.Occurrence)
                    .Select(_ => new ProfilePeriodPattern(
                        Enum.Parse<PeriodType>(_.PeriodType, true),
                        _.Period,
                        _.PeriodStartDate,
                        _.PeriodEndDate,
                        _.PeriodYear,
                        _.Occurrence,
                        _.DistributionPeriod,               
                        string.IsNullOrWhiteSpace(_.PeriodPatternPercentage)
                                ? null
                                : decimal.Parse(
                                    _.PeriodPatternPercentage,
                                    CultureInfo.InvariantCulture),
                _.PeriodPatternCalculationId))
                    .ToArray(),
                ProviderTypeSubTypes = providerTypeSubTypes
                    .Select(_ => new ProviderTypeSubType
                    {
                        ProviderType = _.ProviderType,
                        ProviderSubType = _.ProviderSubType
                    })
                    .ToArray(),
                ReProfilingConfiguration = reProfilingConfiguration
            };
        }

        private static async Task<(EntityModel.FundingPeriod fundingPeriod, EntityModel.FundingStream fundingStream)> GetFundingPeriodAndStream(
            Func<Task<EntityModel.FundingPeriod>> fundingPeriodLookup,
            Func<Task<EntityModel.FundingStream>> fundingStreamLookup)
        {
            var fundingPeriod = await fundingPeriodLookup();
            var fundingStream = await fundingStreamLookup();

            return (fundingPeriod, fundingStream);
        }

        public async Task<FundingStreamPeriodProfilePattern> GetProfilePattern(string fundingPeriodId, string fundingStreamId, string fundingLineCode, string providerType, string providerSubType)
        {
            Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
            Guard.IsNullOrWhiteSpace(fundingLineCode, nameof(fundingLineCode));
            Guard.IsNullOrWhiteSpace(providerType, nameof(providerType));
            Guard.IsNullOrWhiteSpace(providerSubType, nameof(providerSubType));

            var fundingPeriodRepo = _uow.GenericRepository<EntityModel.FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();
            var profilePatternRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();
            var providerTypeSubTypesRepo = _uow.GenericRepository<EntityModel.ProviderTypeSubType>();

            var (fundingPeriod, fundingStream) = await GetFundingPeriodAndStream(
                () => fundingPeriodRepo.SingleOrDefaultAsync(_ => _.FundingPeriodCode == fundingPeriodId),
                () => fundingStreamRepo.SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStreamId));

            if (fundingPeriod == null || fundingStream == null)
            {
                return null;
            }

            var patterns = await profilePatternRepo
                .GetManyAsQueryable(_ =>
                    _.FundingPeriodId == fundingPeriod.FundingPeriodId
                    && _.FundingStreamId == fundingStream.FundingStreamId
                    && _.FundingLineId == fundingLineCode
                    && !_.IsDeleted)
                .ToListAsync();

            if (patterns == null || !patterns.Any())
            {
                return null;
            }

            foreach (var pattern in patterns)
            {
                var providerTypes = await providerTypeSubTypesRepo
                    .GetManyAsQueryable(_ => _.FundingStreamPeriodProfilePatternId == pattern.Id)
                    .ToListAsync();

                if (providerTypes.Any(p =>
                    string.Equals(p.ProviderType, providerType, StringComparison.InvariantCultureIgnoreCase)
                    && string.Equals(p.ProviderSubType, providerSubType, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return await MapProfilePattern(pattern, fundingPeriod, fundingStream);
                }
            }

            return null;
        }

        public async Task<FundingStreamPeriodProfilePattern> GetProfilePattern(string fundingPeriodId, string fundingStreamId, string fundingLineCode, string providerType, DateTimeOffset? DateOpened, string reasonEstablishmentOpened)
        {
            Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
            Guard.IsNullOrWhiteSpace(fundingLineCode, nameof(fundingLineCode));
            Guard.IsNullOrWhiteSpace(providerType, nameof(providerType));
            Guard.ArgumentNotNull(DateOpened, nameof(DateOpened));
            Guard.ArgumentNotNull(reasonEstablishmentOpened, nameof(reasonEstablishmentOpened));

            var fundingPeriodRepo = _uow.GenericRepository<EntityModel.FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();
            var profilePatternRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();

            var (fundingPeriod, fundingStream) = await GetFundingPeriodAndStream(
                () => fundingPeriodRepo.SingleOrDefaultAsync(_ => _.FundingPeriodCode == fundingPeriodId),
                () => fundingStreamRepo.SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStreamId));

            if (fundingPeriod == null || fundingStream == null)
            {
                return null;
            }

            var patterns = await profilePatternRepo
                .GetManyAsQueryable(_ =>
                    _.FundingPeriodId == fundingPeriod.FundingPeriodId
                    && _.FundingStreamId == fundingStream.FundingStreamId
                    && _.FundingLineId == fundingLineCode
                    && !_.IsDeleted)
                .ToListAsync();

            if (patterns == null || !patterns.Any())
            {
                return null;
            }

            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern.ProfilePatternOpenDateConfig))
                {
                    continue;
                }

                var openDateConfigs = JsonConvert.DeserializeObject<IEnumerable<ProfilePatternOpenDateConfiguration>>(pattern.ProfilePatternOpenDateConfig);

                if (openDateConfigs?.Any(p =>
                    string.Equals(p.ProviderType, providerType, StringComparison.InvariantCultureIgnoreCase)
                    && p.OpenReason.Any(reason => string.Equals(reason, reasonEstablishmentOpened, StringComparison.InvariantCultureIgnoreCase))
                    && p.OpenDateStart <= DateOpened.Value
                    && p.OpenDateEnd >= DateOpened.Value) == true)
                {
                    return await MapProfilePattern(pattern, fundingPeriod, fundingStream);
                }
            }

            return null;
        }

        public async Task<IEnumerable<FundingStreamPeriodProfilePattern>> GetProfilePatternsForFundingStreamAndFundingPeriod(string fundingStreamId, string fundingPeriodId)
        {
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
            Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));

            var fundingPeriodRepo = _uow.GenericRepository<EntityModel.FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();
            var profilePatternRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();

            var (fundingPeriod, fundingStream) = await GetFundingPeriodAndStream(
                () => fundingPeriodRepo.SingleOrDefaultAsync(_ => _.FundingPeriodCode == fundingPeriodId),
                () => fundingStreamRepo.SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStreamId));

            if (fundingPeriod == null || fundingStream == null)
            {
                return ArraySegment<FundingStreamPeriodProfilePattern>.Empty;
            }

            var patterns = await profilePatternRepo
                .GetManyAsQueryable(_ => _.FundingPeriodId == fundingPeriod.FundingPeriodId
                    && _.FundingStreamId == fundingStream.FundingStreamId
                    && !_.IsDeleted)
                .ToListAsync();

            if (patterns == null || !patterns.Any())
            {
                return ArraySegment<FundingStreamPeriodProfilePattern>.Empty;
            }

            var results = new List<FundingStreamPeriodProfilePattern>();

            foreach (var pattern in patterns)
            {
                var result = await MapProfilePattern(pattern, fundingPeriod, fundingStream);
                if (result != null)
                {
                    results.Add(result);
                }
            }

            return results.ToArray();

        }



        public async Task<HttpStatusCode> SaveFundingStreamPeriodProfilePattern(FundingStreamPeriodProfilePattern fundingStreamPeriodProfilePattern)
        {
            Guard.ArgumentNotNull(fundingStreamPeriodProfilePattern, nameof(fundingStreamPeriodProfilePattern));

            var fundingPeriodRepo = _uow.GenericRepository<EntityModel.FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<EntityModel.FundingStream>();
            var profilePatternRepo = _uow.GenericRepository<EntityModel.FundingStreamPeriodProfilePattern>();
            var profilePatternPeriodRepo = _uow.GenericRepository<EntityModel.ProfilePatternPeriod>();
            var providerTypeSubTypesRepo = _uow.GenericRepository<EntityModel.ProviderTypeSubType>();
            var reProfilingConfigurationPropertyRepo = _uow.GenericRepository<EntityModel.ReProfilingConfigurationProperty>();

            var (fundingPeriod, fundingStream) = await GetFundingPeriodAndStream(
                () => fundingPeriodRepo.SingleOrDefaultAsync(_ => _.FundingPeriodCode == fundingStreamPeriodProfilePattern.FundingPeriodId),
                () => fundingStreamRepo.SingleOrDefaultAsync(_ => _.FundingStreamCode == fundingStreamPeriodProfilePattern.FundingStreamId));

            if (fundingPeriod == null || fundingStream == null)
            {
                return HttpStatusCode.NotFound;
            }

            var profilePatternEntity = new EntityModel.FundingStreamPeriodProfilePattern()
            {


                Id = fundingStreamPeriodProfilePattern.Id,
                FundingPeriodId = fundingPeriod.FundingPeriodId,
                FundingStreamId = fundingStream.FundingStreamId,
                FundingLineId = fundingStreamPeriodProfilePattern.FundingLineId,
                ProfilePatternKey = fundingStreamPeriodProfilePattern.ProfilePatternKey,
                ProfilePatternType = fundingStreamPeriodProfilePattern.ProfilePatternType?.ToString(),
                RoundingStrategy = fundingStreamPeriodProfilePattern.RoundingStrategy.ToString(),
                FundingStreamPeriodStartDate = fundingStreamPeriodProfilePattern.FundingStreamPeriodStartDate,
                FundingStreamPeriodEndDate = fundingStreamPeriodProfilePattern.FundingStreamPeriodEndDate,
                AllowUserToEditProfilePattern = fundingStreamPeriodProfilePattern.AllowUserToEditProfilePattern,
                ProfilePatternOpenDateConfig = fundingStreamPeriodProfilePattern.ProfilePatternOpenDateConfiguration?.Any() == true
                ? JsonConvert.SerializeObject(fundingStreamPeriodProfilePattern.ProfilePatternOpenDateConfiguration)
                : null,
                ProfilePatternDisplayName = fundingStreamPeriodProfilePattern.ProfilePatternDisplayName,
                ProfilePatternDescription = fundingStreamPeriodProfilePattern.ProfilePatternDescription,
                ProfileCacheEtag = fundingStreamPeriodProfilePattern.ProfileCacheETag,
                Etag = fundingStreamPeriodProfilePattern.ETag,
                IsDeleted = false
            };
            await profilePatternRepo.Upsert(profilePatternEntity, _ => _.Id == profilePatternEntity.Id);

            var profilePatternPeriods = fundingStreamPeriodProfilePattern.ProfilePattern?.Select(_ => new EntityModel.ProfilePatternPeriod
            {
                FundingStreamPeriodProfilePatternId = profilePatternEntity.Id,
                PeriodType = _.PeriodType.ToString(),
                Period = _.Period,
                PeriodStartDate = _.PeriodStartDate,
                PeriodEndDate = _.PeriodEndDate,
                PeriodYear = _.PeriodYear,
                Occurrence = _.Occurrence,
                DistributionPeriod = _.DistributionPeriod,
                PeriodPatternPercentage = _.PeriodPatternPercentage.ToString(),
                PeriodPatternCalculationId = _.PeriodPatternCalculationId
            }).ToList() ?? new List<EntityModel.ProfilePatternPeriod>();

            foreach (var profilePatternPeriod in profilePatternPeriods)
            {
                await profilePatternPeriodRepo.Upsert(profilePatternPeriod, 
                _ => _.FundingStreamPeriodProfilePatternId == profilePatternPeriod.FundingStreamPeriodProfilePatternId
                && string.Equals(_.Period, profilePatternPeriod.Period)
                && string.Equals(_.DistributionPeriod, profilePatternPeriod.DistributionPeriod)
                && _.PeriodYear == profilePatternPeriod.PeriodYear
                && _.Occurrence == profilePatternPeriod.Occurrence);
            }

            var providerTypeSubTypes = fundingStreamPeriodProfilePattern.ProviderTypeSubTypes?.Select(_ => new EntityModel.ProviderTypeSubType
            {
                FundingStreamPeriodProfilePatternId = profilePatternEntity.Id,
                ProviderType = _.ProviderType,
                ProviderSubType = _.ProviderSubType
            }).ToList() ?? new List<EntityModel.ProviderTypeSubType>();

            foreach (var providerTypeSubType in providerTypeSubTypes)
            {
                await providerTypeSubTypesRepo.Upsert(providerTypeSubType, 
                _ => _.FundingStreamPeriodProfilePatternId == providerTypeSubType.FundingStreamPeriodProfilePatternId
                && string.Equals(_.ProviderType, providerTypeSubType.ProviderType)
                && string.Equals(_.ProviderSubType, providerTypeSubType.ProviderSubType));
            }


            var reProfilingConfigurationProperties = BuildReProfilingConfigurationProperties(fundingStreamPeriodProfilePattern.ReProfilingConfiguration, profilePatternEntity.Id);

            foreach (var reProfilingConfigurationPropertie in reProfilingConfigurationProperties)
            {
                await reProfilingConfigurationPropertyRepo.Upsert(reProfilingConfigurationPropertie, 
                    _ => _.FundingStreamPeriodProfilePatternId == reProfilingConfigurationPropertie.FundingStreamPeriodProfilePatternId
                    && string.Equals(_.PropertyName, reProfilingConfigurationPropertie.PropertyName));
            }


            await _uow.CommitAsync();

            return HttpStatusCode.OK;
        }

        private static ProfilePatternReProfilingConfiguration BuildReProfilingConfiguration(
            IEnumerable<EntityModel.ReProfilingConfigurationProperty> reProfilingConfigurationProperties)
        {
            if (reProfilingConfigurationProperties?.Any() != true)
            {
                return null;
            }

            var propertyMap = reProfilingConfigurationProperties
                .ToDictionary(_ => _.PropertyName, _ => _.PropertyValue, StringComparer.OrdinalIgnoreCase);

            return new ProfilePatternReProfilingConfiguration
            {
                ReProfilingEnabled = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.ReProfilingEnabled), out string enabled)
                    && bool.TryParse(enabled, out bool enabledFlag) && enabledFlag,
                IncreasedAmountStrategyKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.IncreasedAmountStrategyKey), out string increased) ? increased : null,
                DecreasedAmountStrategyKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.DecreasedAmountStrategyKey), out string decreased) ? decreased : null,
                SameAmountStrategyKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.SameAmountStrategyKey), out string same) ? same : null,
                InitialFundingStrategyKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.InitialFundingStrategyKey), out string initial) ? initial : null,
                InitialFundingStrategyWithCatchupKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.InitialFundingStrategyWithCatchupKey), out string initialCatchup) ? initialCatchup : null,
                InitialClosureFundingStrategyKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.InitialClosureFundingStrategyKey), out string initialClosure) ? initialClosure : null,
                ConverterFundingStrategyKey = propertyMap.TryGetValue(nameof(ProfilePatternReProfilingConfiguration.ConverterFundingStrategyKey), out string converter) ? converter : null
            };
        }

        private static List<EntityModel.ReProfilingConfigurationProperty> BuildReProfilingConfigurationProperties(
            ProfilePatternReProfilingConfiguration reProfilingConfiguration,
            string profilePatternId)
        {
            var properties = new List<EntityModel.ReProfilingConfigurationProperty>();

            if (reProfilingConfiguration == null)
            {
                return properties;
            }

            properties.Add(new EntityModel.ReProfilingConfigurationProperty
            {
                FundingStreamPeriodProfilePatternId = profilePatternId,
                PropertyName = nameof(ProfilePatternReProfilingConfiguration.ReProfilingEnabled),
                PropertyValue = reProfilingConfiguration.ReProfilingEnabled.ToString()
            });

            void AddProperty(string name, string value)
            {
                if (value == null)
                {
                    return;
                }

                properties.Add(new EntityModel.ReProfilingConfigurationProperty
                {
                    FundingStreamPeriodProfilePatternId = profilePatternId,
                    PropertyName = name,
                    PropertyValue = value
                });
            }

            AddProperty(nameof(ProfilePatternReProfilingConfiguration.IncreasedAmountStrategyKey), reProfilingConfiguration.IncreasedAmountStrategyKey);
            AddProperty(nameof(ProfilePatternReProfilingConfiguration.DecreasedAmountStrategyKey), reProfilingConfiguration.DecreasedAmountStrategyKey);
            AddProperty(nameof(ProfilePatternReProfilingConfiguration.SameAmountStrategyKey), reProfilingConfiguration.SameAmountStrategyKey);
            AddProperty(nameof(ProfilePatternReProfilingConfiguration.InitialFundingStrategyKey), reProfilingConfiguration.InitialFundingStrategyKey);
            AddProperty(nameof(ProfilePatternReProfilingConfiguration.InitialFundingStrategyWithCatchupKey), reProfilingConfiguration.InitialFundingStrategyWithCatchupKey);
            AddProperty(nameof(ProfilePatternReProfilingConfiguration.InitialClosureFundingStrategyKey), reProfilingConfiguration.InitialClosureFundingStrategyKey);
            AddProperty(nameof(ProfilePatternReProfilingConfiguration.ConverterFundingStrategyKey), reProfilingConfiguration.ConverterFundingStrategyKey);

            return properties;
        }
    }
}
