using CalculateFunding.Common.ApiClient.Results.Models;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Messages;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CalculationEntity = CalculateFunding.Repositories.Common.EFCore.EntityModel.Calculation;
using FundingLineResultEntity = CalculateFunding.Repositories.Common.EFCore.EntityModel.FundingLineResult;
using ProviderResult = CalculateFunding.Models.Calcs.ProviderResult;
using ProviderResultEntity = CalculateFunding.Repositories.Common.EFCore.EntityModel.ProviderResult;
using ProviderWithResultsForSpecificationEntity =  CalculateFunding.Services.Results.Models.ProviderWithResultsForSpecifications;

namespace CalculateFunding.Services.Results.Repositories
{
    public class CalculationResultsRepository : ICalculationResultsRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;
        public CalculationResultsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        } 
        
        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(CalculationResultsRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }
        
        public async Task<bool> CheckHasNewResultsForSpecificationIdAndTime(string specificationId, DateTimeOffset dateFrom)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            return  await _uow.GenericRepository<ProviderResultEntity>()
                .GetManyAsQueryable(p => p.SpecificationId == specificationId && !p.IsDeleted && p.UpdatedAt > dateFrom.DateTime)
                .AnyAsync();

        }

        public async Task DeleteCalculationResultsBySpecificationId(string specificationId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            var providerResultRepo = _uow.GenericRepository<ProviderResultEntity>();
            var calcResultRepo = _uow.GenericRepository<CalcResult>();
            var fundingLineRepo = _uow.GenericRepository<FundingLineResultEntity>();
            var providerSpecRepo = _uow.GenericRepository<ProviderSpecificationResult>();

            // Get provider results for the specification
            var providerResults = providerResultRepo.GetManyAsQueryable(x => x.SpecificationId == specificationId).ToList();

            if (!providerResults.Any())
            {
                return;
            }

            var providerResultIds = providerResults.Select(x => x.ProviderResultId).ToList();

            if (deletionType == DeletionType.SoftDelete)
            {
                // Soft delete - mark provider results as deleted
                foreach (var result in providerResults)
                {
                    result.IsDeleted = true;
                    providerResultRepo.Update(result);
                }
            }

            if (deletionType == DeletionType.PermanentDelete)
            {
                // Hard delete - remove related calc and funding line results, provider specification entries and provider results
                if (providerResultIds.Any())
                {
                    calcResultRepo.Delete(x => providerResultIds.Contains(x.ProviderResultId));
                    fundingLineRepo.Delete(x => providerResultIds.Contains(x.ProviderResultId));
                }

                // Remove provider specification rows for this specification
                providerSpecRepo.Delete(x => x.SpecificationId == specificationId);

                // Remove the provider result rows
                providerResultRepo.Delete(x => x.SpecificationId == specificationId);
            }

            await _uow.CommitAsync();
        }

        public async Task DeleteCurrentProviderResults(IEnumerable<ProviderResult> providerResults)
        {
            Guard.ArgumentNotNull(providerResults, nameof(providerResults));
            var providerResultRepo = _uow.GenericRepository<ProviderResultEntity>();
            var calcResultRepo = _uow.GenericRepository<CalcResult>();
            var fundingLineRepo = _uow.GenericRepository<FundingLineResultEntity>();
            var providerSpecRepo = _uow.GenericRepository<ProviderSpecificationResult>();

            var providerResultIds = providerResults?.Select(x => x.Id).ToList() ?? new List<string>();

            if (!providerResultIds.Any())
            {
                return;
            }

            var entitiesToUpdate = await providerResultRepo.GetManyAsQueryable(x => providerResultIds.Contains(x.ProviderResultId)).ToListAsync();

            foreach (var entity in entitiesToUpdate)
            {
                if (providerResultIds.Any())
                {
                    calcResultRepo.Delete(x => providerResultIds.Contains(x.ProviderResultId));
                    fundingLineRepo.Delete(x => providerResultIds.Contains(x.ProviderResultId));
                }

                providerSpecRepo.Delete(x => x.SpecificationId == entity.SpecificationId && x.ProviderId == entity.ProviderId);

                providerResultRepo.Delete(x => x.ProviderResultId == entity.ProviderResultId);
            }

            await _uow.CommitAsync();
        }       
        
        public async Task<IEnumerable<DocumentEntity<ProviderResult>>> GetAllProviderResults()
        {
            var providerResultsList = new List<DocumentEntity<ProviderResult>>();

            var providerResultsResponse =  _uow.GenericRepository<ProviderResultEntity>().
                GetManyAsQueryable(_=>!_.IsDeleted);

            foreach (var providerResultEntity in providerResultsResponse)
            {
                ProviderResult providerResult = await BuildProviderResult(providerResultEntity);

                var documentEntity = new DocumentEntity<ProviderResult>
                {
                    CreatedAt = providerResultEntity.CreatedAt,
                    UpdatedAt = providerResultEntity.UpdatedAt,
                    Content = providerResult
                };

                providerResultsList.Add(documentEntity);
            }

            return providerResultsList;
        }
        
        public async Task<ProviderResult> GetProviderResult(string providerId, string specificationId)
        {
            Guard.ArgumentNotNull(providerId, nameof(providerId));
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var providerResultResponce = await _uow.GenericRepository<ProviderResultEntity>().FirstOrDefaultAsync(x => x.ProviderId == providerId && x.SpecificationId == specificationId && !x.IsDeleted);

            return await BuildProviderResult(providerResultResponce);
        }
        
        public async Task<ProviderResult> GetProviderResultById(string providerResultId, string partitionKey)
        {
            Guard.ArgumentNotNull(providerResultId, nameof(providerResultId));

            var providerResultResponce = await _uow.GenericRepository<ProviderResultEntity>().FirstOrDefaultAsync(x => x.ProviderResultId == providerResultId && !x.IsDeleted);

            return await BuildProviderResult(providerResultResponce);
        } 
        
        public async Task<IEnumerable<ProviderResult>> GetProviderResultsBySpecificationId(string specificationId, int maxItemCount = -1)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var query = _uow.GenericRepository<ProviderResultEntity>().GetManyAsQueryable(x => x.SpecificationId == specificationId && !x.IsDeleted);

            if (maxItemCount > 0)
            {
                query = query.Take(maxItemCount);
            }

            var providerResultsResponse = await query.ToListAsync();

            var providerResults = new List<ProviderResult>();

            foreach (var providerResult in providerResultsResponse)
            {
                providerResults.Add(await BuildProviderResult(providerResult));
            }

            return providerResults;
        }   
        
        public async Task<ProviderResult> GetProviderResultByCalculationType(string providerId, string specificationId, CalculateFunding.Models.Calcs.CalculationType calculationType)
        {
            Guard.ArgumentNotNull(providerId, nameof(providerId));
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var providerResultResponse = await _uow.GenericRepository<ProviderResultEntity>().FirstOrDefaultAsync(x => x.SpecificationId == specificationId && x.ProviderId == providerId && !x.IsDeleted);

            if (providerResultResponse == null)
            {
                return null;
            }

            ProviderResult result = await BuildProviderResult(providerResultResponse);

            result?.CalculationResults
                .RemoveAll(x => x.CalculationType != calculationType);

            return result;
        } 
        
        public async Task<IEnumerable<ProviderResult>> GetProviderResultsBySpecificationIdAndProviders(IEnumerable<string> providerIds, string specificationId)
        {
            Guard.ArgumentNotNull(providerIds, nameof(providerIds));
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var providerResults = new List<ProviderResult>(); 
            
            var providerResultsResponse = await _uow.GenericRepository<ProviderResultEntity>().GetManyAsQueryable(x => x.SpecificationId == specificationId && providerIds
                .Contains(x.ProviderId) && !x.IsDeleted).ToListAsync();


            foreach (var providerResult in providerResultsResponse)
            {
                providerResults.Add(await BuildProviderResult(providerResult));
            }

            return providerResults;
        }
        
        public async Task<ProviderResult> GetSingleProviderResultBySpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var providerResultResponce = await _uow.GenericRepository<ProviderResultEntity>().FirstOrDefaultAsync(x => x.SpecificationId == specificationId && !x.IsDeleted);
            
            return providerResultResponce != null ? await BuildProviderResult(providerResultResponce) : null;
        } 
        
        public async Task<IEnumerable<ProviderResult>> GetSpecificationResults(string providerId)
        {
            Guard.ArgumentNotNull(providerId, nameof(providerId));
            var providerResults = new List<ProviderResult>();

            var providerResultsResponce = await _uow.GenericRepository<ProviderResultEntity>().GetManyAsQueryable(x => x.ProviderId == providerId && !x.IsDeleted).ToListAsync();

            foreach (var providerResult in providerResultsResponce)
            {
                var providerResultData = await BuildProviderResult(providerResult);
                string resultsString = JsonConvert.SerializeObject(providerResultData);

                resultsString = resultsString.ConvertExpotentialNumber();

                var cleanedEntity = JsonConvert.DeserializeObject<ProviderResult>(resultsString);
                providerResults.Add(cleanedEntity);
            }

            return providerResults;
        }  
        
        public async Task<ProviderWithResultsForSpecificationEntity> GetProviderWithResultsForSpecificationsByProviderId(string providerId)
        {
            Guard.ArgumentNotNull(providerId, nameof(providerId));

            var fundingPeriodRepo = _uow.GenericRepository<FundingPeriod>();
            var fundingStreamRepo = _uow.GenericRepository<FundingStream>();
            var providerSpecResultRepo = _uow.GenericRepository<ProviderSpecificationResult>();

            var providerSpecResults = await providerSpecResultRepo
                .GetManyAsQueryable(x => x.ProviderId == providerId && !x.IsDeleted)
                .ToListAsync();

            var fundingStreamIds = providerSpecResults.SelectMany(x => x.FundingStreamIds.ToString().Split(',')).Distinct().ToList();
            var fundingPeriodIds = providerSpecResults.Select(x => x.FundingPeriodId).Distinct().ToList();

            var fundingStreams = await fundingStreamRepo.GetManyAsQueryable(x => fundingStreamIds.Contains(x.FundingStreamId.ToString())).ToListAsync();
            var fundingPeriods = await fundingPeriodRepo.GetManyAsQueryable(x => fundingPeriodIds.Contains(x.FundingPeriodId)).ToListAsync();

            return new ProviderWithResultsForSpecificationEntity
            {
                Provider = new Models.ProviderInformation
                {
                    Id = providerSpecResults.First().ProviderId,
                },
                Specifications = providerSpecResults.Select(psr => new Models.SpecificationInformation
                {
                    Id = psr.SpecificationId,
                    Name = psr.SpecificationName,
                    LastEditDate = psr.UpdatedAt,
                    FundingPeriodId = fundingPeriods.FirstOrDefault(_=>_.FundingPeriodId == psr.FundingPeriodId)?.FundingPeriodCode,
                    FundingStreamIds = new List<string> { fundingStreams.FirstOrDefault(_=>_.FundingStreamId == psr.FundingStreamIds)?.FundingStreamCode },
                    FundingPeriodEnd = psr.FundingPeriodEnd,
                }).ToList()
            };
        }

        public async Task<DateTime?> GetSpecificationCalculationResultsLastUpdated(string specificationId) //check
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var latest = await _uow.GenericRepository<ProviderResultEntity>()
                .GetManyAsQueryable(x => x.SpecificationId == specificationId && !x.IsDeleted).OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync();

            return latest?.UpdatedAt;
        }

        public async Task<bool> ProviderHasResultsBySpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            return await _uow.GenericRepository<ProviderResultEntity>().GetManyAsQueryable(x => x.SpecificationId == specificationId && !x.IsDeleted).AnyAsync();
        } 

        public async Task<HttpStatusCode> UpdateProviderResults(List<ProviderResult> providerResults)
        {
            Guard.ArgumentNotNull(providerResults, nameof(providerResults));

                foreach (var providerResult in providerResults)
                {
                    // Upsert ProviderResultEntity
                    var pr = new ProviderResultEntity
                    {
                        ProviderResultId = providerResult.Id,
                        ProviderId = providerResult.Provider?.Id,
                        ProviderVersionId = providerResult.Provider?.Id,
                        SpecificationId = providerResult.SpecificationId,
                        IsIndicativeProvider = providerResult.IsIndicativeProvider ?? false,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedAt = providerResult.CreatedAt == default ? DateTime.UtcNow : providerResult.CreatedAt.UtcDateTime
                    };

                    await _uow.GenericRepository<ProviderResultEntity>().Upsert(pr, x => x.ProviderResultId == pr.ProviderResultId);

                    // Upsert Provider (basic fields)
                    if (providerResult.Provider != null)
                    {
                        var providerEntity = new Provider
                        {
                            ProviderId = providerResult.Provider.Id,
                            ProviderVersionId = providerResult.Provider.Id,
                            Ukprn = providerResult.Provider.UKPRN,
                            Urn = providerResult.Provider.URN,
                            Upin = providerResult.Provider.UPIN,
                            EstablishmentNumber = providerResult.Provider.EstablishmentNumber,
                            DfeEstablishmentNumber = providerResult.Provider.DfeEstablishmentNumber,
                            Authority = providerResult.Provider.Authority,
                            ProviderType = providerResult.Provider.ProviderType,
                            ProviderSubType = providerResult.Provider.ProviderSubType,
                            DateOpened = providerResult.Provider.DateOpened?.UtcDateTime,
                            DateClosed = providerResult.Provider.DateClosed?.UtcDateTime,
                            ProviderProfileIdType = providerResult.Provider.ProviderProfileIdType,
                            LaCode = providerResult.Provider.LACode,
                            LaOrg = providerResult.Provider.LAOrg,
                            NavVendorNo = providerResult.Provider.NavVendorNo,
                            CrmAccountId = providerResult.Provider.CrmAccountId,
                            LegalName = providerResult.Provider.LegalName,
                            Status = providerResult.Provider.Status,
                            PhaseOfEducation = providerResult.Provider.PhaseOfEducation,
                            ReasonEstablishmentClosed = providerResult.Provider.ReasonEstablishmentClosed,
                            Successor = providerResult.Provider.Successor,
                            TrustStatus = providerResult.Provider.TrustStatus.ToString(),
                            TrustCode = providerResult.Provider.TrustCode,
                            TrustName = providerResult.Provider.TrustName,
                            Town = providerResult.Provider.Town,
                            Postcode = providerResult.Provider.Postcode,
                            CompaniesHouseNumber = providerResult.Provider.CompaniesHouseNumber,
                            GroupIdNumber = providerResult.Provider.GroupIdNumber,
                            RscRegionName = providerResult.Provider.RscRegionName,
                            RscRegionCode = providerResult.Provider.RscRegionCode,
                            GovernmentOfficeRegionName = providerResult.Provider.GovernmentOfficeRegionName,
                            GovernmentOfficeRegionCode = providerResult.Provider.GovernmentOfficeRegionCode,
                            DistrictName = providerResult.Provider.DistrictName,
                            DistrictCode = providerResult.Provider.DistrictCode,
                            WardCode = providerResult.Provider.WardCode,
                            WardName = providerResult.Provider.WardName,
                            CensusWardName = providerResult.Provider.CensusWardName,
                            CensusWardCode = providerResult.Provider.CensusWardCode,
                            MiddleSuperOutputAreaCode = providerResult.Provider.MiddleSuperOutputAreaCode,
                            MiddleSuperOutputAreaName = providerResult.Provider.MiddleSuperOutputAreaName,
                            LowerSuperOutputAreaCode = providerResult.Provider.LowerSuperOutputAreaCode,
                            LowerSuperOutputAreaName = providerResult.Provider.LowerSuperOutputAreaName,
                            ParliamentaryConstituencyCode = providerResult.Provider.ParliamentaryConstituencyCode,
                            ParliamentaryConstituencyName = providerResult.Provider.ParliamentaryConstituencyName,
                            LondonRegionCode = providerResult.Provider.LondonRegionCode,
                            LondonRegionName = providerResult.Provider.LondonRegionName,
                            CountyCode = providerResult.Provider.CountryCode,
                            CountyName = providerResult.Provider.CountryName,
                            LocalGovernmentGroupTypeCode = providerResult.Provider.LocalGovernmentGroupTypeCode,
                            LocalGovernmentGroupTypeName = providerResult.Provider.LocalGovernmentGroupTypeName,
                            Street = providerResult.Provider.Street,
                            Locality = providerResult.Provider.Locality,
                            Address3 = providerResult.Provider.Address3,
                            PaymentOrganisationIdentifier = providerResult.Provider.PaymentOrganisationIdentifier,
                            PaymentOrganisationName = providerResult.Provider.PaymentOrganisationName,
                            ProviderTypeCode = providerResult.Provider.ProviderTypeCode,
                            ProviderSubTypeCode = providerResult.Provider.ProviderSubTypeCode,
                            PreviousLaCode = providerResult.Provider.PreviousLaCode,
                            PreviousLaName = providerResult.Provider.PreviousLaName,
                            PreviousEstablishmentNumber = providerResult.Provider.PreviousEstablishmentNumber,
                            FurtherEducationTypeCode = providerResult.Provider.FurtherEducationTypeCode,
                            FurtherEducationTypeName = providerResult.Provider.FurtherEducationTypeName,
                            PhaseOfEducationCode = providerResult.Provider.PhaseOfEducationCode,
                            StatutoryLowAge = providerResult.Provider.StatutoryLowAge,
                            StatutoryHighAge = providerResult.Provider.StatutoryHighAge,
                            OfficialSixthFormCode = providerResult.Provider.OfficialSixthFormCode,
                            OfficialSixthFormName = providerResult.Provider.OfficialSixthFormName,
                            StatusCode = providerResult.Provider.StatusCode,
                            ReasonEstablishmentOpenedCode = providerResult.Provider.ReasonEstablishmentOpenedCode,
                            ReasonEstablishmentClosedCode = providerResult.Provider.ReasonEstablishmentClosedCode
                        };

                        await _uow.GenericRepository<Provider>().Upsert(providerEntity, x => x.ProviderId == providerEntity.ProviderId && x.ProviderVersionId == providerEntity.ProviderVersionId);
                    }

                    if (providerResult.CalculationResults != null)
                    {
                        foreach (var calcResult in providerResult.CalculationResults)
                        {
                            var cr = new CalcResult
                            {
                                CalculationId = calcResult.Calculation?.Id,
                                CalculationName = calcResult.Calculation?.Name,
                                Value = calcResult.Value?.ToString(),
                                ExceptionType = calcResult.ExceptionType,
                                ExceptionMessage = calcResult.ExceptionMessage,
                                ExceptionStackTrace = calcResult.ExceptionStackTrace,
                                CalculationType = calcResult.CalculationType.ToString(),
                                CalculationDataType = calcResult.CalculationDataType.ToString(),
                                ProviderResultId = pr.ProviderResultId
                            };

                            await _uow.GenericRepository<CalcResult>().Upsert(cr, x => x.CalculationId == cr.CalculationId && x.ProviderResultId == cr.ProviderResultId);
                        }
                    }

                    if (providerResult.FundingLineResults != null)
                    {
                        foreach (var fundingLineResult in providerResult.FundingLineResults)
                        {
                            var fr = new FundingLineResultEntity
                            {
                                FundingLineId = fundingLineResult.FundingLine?.Id,
                                FundingLineName = fundingLineResult.FundingLine?.Name,
                                FundingLineFundingStreamId = fundingLineResult.FundingLineFundingStreamId,
                                Value = fundingLineResult.Value?.ToString(),
                                ExceptionType = fundingLineResult.ExceptionType,
                                ExceptionMessage = fundingLineResult.ExceptionMessage,
                                ExceptionStackTrace = fundingLineResult.ExceptionStackTrace,
                                ProviderResultId = pr.ProviderResultId
                            };

                            await _uow.GenericRepository<FundingLineResultEntity>().Upsert(fr, x => x.FundingLineId == fr.FundingLineId && x.ProviderResultId == fr.ProviderResultId);
                        }
                    }
                }

                await _uow.CommitAsync();

                return HttpStatusCode.OK;
        }

        public async Task UpsertSpecificationWithProviderResults(params Models.ProviderWithResultsForSpecifications[] providerWithResultsForSpecifications)
        {
            Guard.ArgumentNotNull(providerWithResultsForSpecifications, nameof(providerWithResultsForSpecifications));
            var fundingStreamRepo = _uow.GenericRepository<FundingStream>();
            var fundingPeriodRepo = _uow.GenericRepository<FundingPeriod>();

            foreach (var providerWithResults in providerWithResultsForSpecifications)
            {
                if (providerWithResults?.Provider == null || providerWithResults.Specifications == null)
                {
                    continue;
                }

                foreach (var spec in providerWithResults.Specifications)
                {
                    if (spec == null)
                    {
                        continue;
                    }

                    var psr = new ProviderSpecificationResult()
                    {
                        ProviderId = providerWithResults.Provider.Id,
                        SpecificationId = spec.Id,
                        SpecificationName = spec.Name,
                        LastEditDate = spec.LastEditDate?.UtcDateTime,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    var fundingPeriodId = fundingPeriodRepo.FirstOrDefaultAsync(x => x.FundingPeriodCode == spec.FundingPeriodId).Result?.FundingPeriodId;

                    psr.FundingPeriodId = (int)fundingPeriodId;

                    var fundingStreamIds = fundingStreamRepo.FirstOrDefaultAsync(x => x.FundingStreamCode == spec.FundingStreamIds.FirstOrDefault()).Result?.FundingStreamId;
                    
                    psr.FundingStreamIds = (int)fundingStreamIds;

                    if (spec.FundingPeriodEnd.HasValue)
                    {
                        psr.FundingPeriodEnd = spec.FundingPeriodEnd.Value.UtcDateTime;
                    }

                    await _uow.GenericRepository<ProviderSpecificationResult>().Upsert(psr, x => x.ProviderId == psr.ProviderId && x.SpecificationId == psr.SpecificationId);
                }
            }            
            await _uow.CommitAsync();
        }


        public async Task ProviderResultsBatchProcessing(string specificationId,Func<List<ProviderResult>, Task> processProcessProviderResultsBatch,int itemsPerPage = 1000)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(processProcessProviderResultsBatch, nameof(processProcessProviderResultsBatch));

            var query = _uow.GenericRepository<ProviderResultEntity>()
                .GetManyAsQueryable(x =>
                    x.SpecificationId == specificationId &&
                    !x.IsDeleted)
                .OrderBy(x => x.Provider.Ukprn);

            int page = 0;

            while (true)
            {
                var batch = await query
                    .Skip(page * itemsPerPage)
                    .Take(itemsPerPage)
                    .ToListAsync();

                if (!batch.Any())
                    break;

                var results = new List<ProviderResult>();

                foreach (var item in batch)
                {
                    results.Add(await BuildProviderResult(item));
                }

                await processProcessProviderResultsBatch(results);
                page++;
            }
        }

        public async Task<IEnumerable<AggregateCalculationResults>> GetAggregateCalculationResults(string specificationId, IEnumerable<string> calculationIds)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(calculationIds, nameof(calculationIds));

            var calcValuePairs = await _uow
                .GenericRepository<ProviderResultEntity>()
                .GetManyAsQueryable(x =>
                    x.SpecificationId == specificationId && !x.IsDeleted &&
                    x.CalcResults.Any())
                .SelectMany(x => x.CalcResults)
                .Where(r =>
                    calculationIds.Contains(r.CalculationId) &&
                    r.Value != null)
                .Select(r => new { r.CalculationId, r.Value })
                .ToListAsync();

            var results = calcValuePairs
                .Select(p => new { p.CalculationId, Value = decimal.TryParse(p.Value, out var v) ? (decimal?)v : null })
                .Where(p => p.Value.HasValue)
                .GroupBy(p => p.CalculationId)
                .Select(g => new AggregateCalculationResults
                {
                    CalculationId = g.Key,
                    SumValue = g.Sum(x => x.Value.Value),
                    AvgValue = g.Average(x => x.Value.Value),
                    MinValue = g.Min(x => x.Value.Value),
                    MaxValue = g.Max(x => x.Value.Value)
                })
                .ToList();

            return results;
        }

        public ICosmosDbFeedIterator GetProvidersWithResultsForSpecificationBySpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var providerSpecRepo = _uow.GenericRepository<ProviderSpecificationResult>();
            var fundingStreamRepo = _uow.GenericRepository<FundingStream>();
            var fundingPeriodRepo = _uow.GenericRepository<FundingPeriod>();

            var providerSpecQuery = providerSpecRepo.GetManyAsQueryable(x => x.SpecificationId == specificationId && !x.IsDeleted);
            var fundingStreamQuery = fundingStreamRepo.GetManyAsQueryable(_ => true);
            var fundingPeriodQuery = fundingPeriodRepo.GetManyAsQueryable(_ => true);

            return new SqlProvidersWithResultsFeedIterator(providerSpecQuery, fundingStreamQuery, fundingPeriodQuery);
        }

        public async Task<decimal> GetCalculationResultTotalForSpecificationId(string specificationId) 
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var calcResults = await _uow.GenericRepository<CalcResult>().GetManyAsQueryable(
                cr => cr.Value != null && _uow.GenericRepository<CalculationEntity>().GetManyAsQueryable(
                    c => c.SpecificationId == specificationId && c.CalculationId == cr.CalculationId).Any()).ToListAsync();

            var values = new List<decimal>();

            foreach (var result in calcResults)
            {
                bool isTargetCalculationType = false;

                if (!string.IsNullOrWhiteSpace(result.CalculationType))
                {
                    if (int.TryParse(result.CalculationType, out var numericType))
                    {
                        isTargetCalculationType = numericType == 10;
                    }
                    else
                    {
                        if (Enum.TryParse<CalculateFunding.Models.Calcs.CalculationType>(result.CalculationType, true, out var enumType))
                        {
                            isTargetCalculationType = ((int)enumType) == 10;
                        }
                    }
                }

                if (isTargetCalculationType && decimal.TryParse(result.Value, out var decimalValue))
                {
                    values.Add(decimalValue);
                }
            }

            return values.Sum();
        }



        #region private helper methods

        private async Task<ProviderResult> BuildProviderResult(ProviderResultEntity providerResultResponce)
        {
            var calcResultResponce = await _uow.GenericRepository<CalcResult>().GetManyAsQueryable(x => x.ProviderResultId == providerResultResponce.ProviderResultId).ToListAsync();
            var fundingLineResultsResponce = await _uow.GenericRepository<FundingLineResultEntity>().GetManyAsQueryable(x => x.ProviderResultId == providerResultResponce.ProviderResultId).ToListAsync();
            var providerReponce = await _uow.GenericRepository<Provider>().FirstOrDefaultAsync(x => x.ProviderId == providerResultResponce.ProviderId && x.ProviderVersionId == providerResultResponce.ProviderVersionId);
            var successorResponse = await _uow.GenericRepository<Successor>().GetManyAsQueryable(x => x.ProviderId == providerResultResponce.ProviderId && x.ProviderVersionId == providerResultResponce.ProviderVersionId).Select(_=>_.SuccessorId.ToString()).ToListAsync();
            var predecessorResponse = await _uow.GenericRepository<Predecessor>().GetManyAsQueryable(x => x.ProviderId == providerResultResponce.ProviderId && x.ProviderVersionId == providerResultResponce.ProviderVersionId).Select(_ => _.PredecessorId.ToString()).ToListAsync();
            return new ProviderResult
            {
                Id = providerResultResponce.ProviderResultId,
                CreatedAt = providerResultResponce.CreatedAt,
                SpecificationId = providerResultResponce.SpecificationId,
                IsIndicativeProvider = providerResultResponce.IsIndicativeProvider,

                Provider = new CalculateFunding.Models.ProviderLegacy.ProviderSummary
                {
                    Id = providerResultResponce.ProviderId,
                    Name = providerReponce.Name,
                    ReasonEstablishmentOpened = providerReponce.ReasonEstablishmentOpened,
                    URN = providerReponce.Urn,
                    UKPRN = providerReponce.Ukprn,
                    UPIN = providerReponce.Upin,
                    EstablishmentNumber = providerReponce.EstablishmentNumber,
                    DfeEstablishmentNumber = providerReponce.DfeEstablishmentNumber,
                    Authority = providerReponce.Authority,
                    ProviderType = providerReponce.ProviderType,
                    ProviderSubType = providerReponce.ProviderSubType,
                    DateOpened = providerReponce.DateOpened,
                    DateClosed = providerReponce.DateClosed,
                    ProviderProfileIdType = providerReponce.ProviderProfileIdType,
                    LACode = providerReponce.LaCode,
                    LAOrg = providerReponce.LaOrg,
                    NavVendorNo = providerReponce.NavVendorNo,
                    CrmAccountId = providerReponce.CrmAccountId,
                    LegalName = providerReponce.LegalName,
                    Status = providerReponce.Status,
                    PhaseOfEducation = providerReponce.PhaseOfEducation,
                    ReasonEstablishmentClosed = providerReponce.ReasonEstablishmentClosed,
                    Successor = providerReponce.Successor,
                    TrustStatus = !string.IsNullOrWhiteSpace(providerReponce.TrustStatus) &&
                          System.Enum.TryParse<CalculateFunding.Models.ProviderLegacy.TrustStatus>(providerReponce.TrustStatus, true, out var parsedTrustStatus)
                          ? parsedTrustStatus
                          : CalculateFunding.Models.ProviderLegacy.TrustStatus.NotApplicable,
                    TrustCode = providerReponce.TrustCode,
                    TrustName = providerReponce.TrustName,
                    Town = providerReponce.Town,
                    Postcode = providerReponce.Postcode,
                    CompaniesHouseNumber = providerReponce.CompaniesHouseNumber,
                    GroupIdNumber = providerReponce.GroupIdNumber,
                    RscRegionName = providerReponce.RscRegionName,
                    RscRegionCode = providerReponce.RscRegionCode,
                    GovernmentOfficeRegionName = providerReponce.GovernmentOfficeRegionName,
                    GovernmentOfficeRegionCode = providerReponce.GovernmentOfficeRegionCode,
                    DistrictName = providerReponce.DistrictName,
                    DistrictCode = providerReponce.DistrictCode,
                    WardCode = providerReponce.WardCode,
                    WardName = providerReponce.WardName,
                    CensusWardName = providerReponce.CensusWardName,
                    CensusWardCode = providerReponce.CensusWardCode,
                    MiddleSuperOutputAreaCode = providerReponce.MiddleSuperOutputAreaCode,
                    MiddleSuperOutputAreaName = providerReponce.MiddleSuperOutputAreaName,
                    LowerSuperOutputAreaCode = providerReponce.LowerSuperOutputAreaCode,
                    LowerSuperOutputAreaName = providerReponce.LowerSuperOutputAreaName,
                    ParliamentaryConstituencyCode = providerReponce.ParliamentaryConstituencyCode,
                    ParliamentaryConstituencyName = providerReponce.ParliamentaryConstituencyName,
                    LondonRegionCode = providerReponce.LondonRegionCode,
                    LondonRegionName = providerReponce.LondonRegionName,
                    CountryCode = providerReponce.CountyCode,
                    CountryName = providerReponce.CountyName,
                    LocalGovernmentGroupTypeCode = providerReponce.LocalGovernmentGroupTypeCode,
                    LocalGovernmentGroupTypeName = providerReponce.LocalGovernmentGroupTypeName,
                    Street = providerReponce.Street,
                    Locality = providerReponce.Locality,
                    Address3 = providerReponce.Address3,
                    PaymentOrganisationIdentifier = providerReponce.PaymentOrganisationIdentifier,
                    PaymentOrganisationName = providerReponce.PaymentOrganisationName,                    
                    ProviderTypeCode = providerReponce.ProviderTypeCode,
                    ProviderSubTypeCode = providerReponce.ProviderSubTypeCode,
                    PreviousLaCode = providerReponce.PreviousLaCode,
                    PreviousLaName = providerReponce.PreviousLaName,
                    PreviousEstablishmentNumber = providerReponce.PreviousEstablishmentNumber,
                    FurtherEducationTypeCode = providerReponce.FurtherEducationTypeCode,
                    FurtherEducationTypeName = providerReponce.FurtherEducationTypeName,
                    Predecessors = predecessorResponse.Any() ? predecessorResponse : null,
                    Successors = successorResponse.Any() ? successorResponse : null,
                    PhaseOfEducationCode = providerReponce?.PhaseOfEducationCode,
                    StatutoryLowAge = providerReponce?.StatutoryLowAge,
                    StatutoryHighAge = providerReponce?.StatutoryHighAge,
                    OfficialSixthFormCode = providerReponce.OfficialSixthFormCode,
                    OfficialSixthFormName = providerReponce.OfficialSixthFormName,
                    StatusCode = providerReponce?.StatusCode,
                    ReasonEstablishmentOpenedCode = providerReponce.ReasonEstablishmentOpenedCode,
                    ReasonEstablishmentClosedCode = providerReponce.ReasonEstablishmentClosedCode,
                },
                CalculationResults = calcResultResponce
                    .Select(cr => new CalculateFunding.Models.Calcs.CalculationResult
                    {
                        Calculation = new Reference
                        {
                            Id = cr.CalculationId,
                            Name = cr.CalculationName
                        },
                        Value = decimal.TryParse(cr.Value, out var value) ? value : (decimal?)null,
                        ExceptionType = cr.ExceptionType,
                        ExceptionMessage = cr.ExceptionMessage,
                        ExceptionStackTrace = cr.ExceptionStackTrace,
                        CalculationType = System.Enum.Parse<CalculateFunding.Models.Calcs.CalculationType>(cr.CalculationType),
                        CalculationDataType = System.Enum.Parse<CalculateFunding.Models.Calcs.CalculationDataType>(cr.CalculationDataType)
                    }).ToList(),

                FundingLineResults = fundingLineResultsResponce
                    .Select(fr => new CalculateFunding.Models.Calcs.FundingLineResult
                    {
                        FundingLine = new Reference
                        {
                            Id = fr.FundingLineId,
                            Name = fr.FundingLineName
                        },
                        FundingLineFundingStreamId = fr.FundingLineFundingStreamId,
                        Value = decimal.TryParse(fr.Value, out var value) ? value : (decimal?)null,
                        ExceptionType = fr.ExceptionType,
                        ExceptionMessage = fr.ExceptionMessage,
                        ExceptionStackTrace = fr.ExceptionStackTrace
                    }).ToList()
            };
        }

        #endregion
    }
}
