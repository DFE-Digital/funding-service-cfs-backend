using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Calcs.ObsoleteItems;
using CalculateFunding.Models.Messages;
using CalculateFunding.Services.Calcs.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using PublishStatus = CalculateFunding.Models.Versioning.PublishStatus;

namespace CalculateFunding.Services.Calcs
{
    public class CalculationsRepository : ICalculationsRepository, IHealthChecker
    {

        protected readonly IUnitOfWork _uow;

        public CalculationsRepository(IUnitOfWork uow)
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

        public bool IsConnectedToSQL()
        {
            return true;
        }

        public async Task<HttpStatusCode> CreateDraftCalculation(Calculation calculation)
        {
            Guard.ArgumentNotNull(calculation, nameof(calculation));

            var calculationRepo = _uow.GenericRepository<EntityModel.Calculation>();

            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>()
                .SingleOrDefaultAsync(_ => _.FundingStreamCode == calculation.FundingStreamId);

            var calculationEntity = new EntityModel.Calculation()
            {
                CalculationId = calculation.Id,
                SpecificationId = calculation.SpecificationId,
                FundingStreamId = fundingStream.FundingStreamId,
                IsDeleted = false
            };

            calculationRepo.Insert(calculationEntity);

            return HttpStatusCode.Created;
        }

        public async Task<HttpStatusCode> CreateObsoleteItem(ObsoleteItem obsoleteItem)
        {
            Guard.ArgumentNotNull(obsoleteItem, nameof(obsoleteItem));

            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>()
                .SingleOrDefaultAsync(_ => _.FundingStreamCode == obsoleteItem.FundingStreamId);

            var obsoleteItemRepo = _uow.GenericRepository<EntityModel.ObsoleteItem>();
            var obsoleteItemCalcsRepo = _uow.GenericRepository<EntityModel.ObsoleteItemCalc>();

            var obsoleteItemEntity = new EntityModel.ObsoleteItem()
            {
                ObsoleteItemId = obsoleteItem.Id,
                SpecificationId = obsoleteItem.SpecificationId,
                DatasetRelationshipId = obsoleteItem.DatasetRelationshipId,
                DatasetRelationshipName = obsoleteItem.DatasetRelationshipName,
                DatasetFieldId = obsoleteItem.DatasetFieldId,
                DatasetFieldName = obsoleteItem.DatasetFieldName,
                DatasetDataType = obsoleteItem.DatasetDatatype.ToString(),
                IsReleasedData = obsoleteItem.IsReleasedData,
                RelationshipName = obsoleteItem.RelationshipName,
                ItemType = obsoleteItem.ItemType.ToString(),
                EnumValueName = obsoleteItem.EnumValueName,
                FundingLineId = (int?)obsoleteItem.FundingLineId,
                FundingStreamId = fundingStream.FundingStreamId,
                TemplateCalculationId = (int?)obsoleteItem.TemplateCalculationId,
                CodeReference = obsoleteItem.CodeReference,
                FundingLineName = obsoleteItem.FundingLineName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsDeleted = false
            };

            obsoleteItemRepo.Insert(obsoleteItemEntity);

            if (obsoleteItem.CalculationIds.Any())
            {
                List<EntityModel.ObsoleteItemCalc> obsoleteItemCalcsEntities = obsoleteItem.CalculationIds.Select(_
                => new EntityModel.ObsoleteItemCalc()
                {
                    ObsoleteItemId = obsoleteItem.Id,
                    CalculationId = _
                }).ToList();

                obsoleteItemCalcsRepo.BulkInsertAsync(obsoleteItemCalcsEntities);
            }

            await _uow.CommitAsync();

            return HttpStatusCode.Created;
        }

        public async Task DeleteCalculationsBySpecificationId(string specificationId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(deletionType, nameof(deletionType));

            var calculationRepo = _uow.GenericRepository<EntityModel.Calculation>();
            var calculationVersionRepo = _uow.GenericRepository<EntityModel.CalculationVersion>();

            var calculations = await calculationRepo.GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();

            if(deletionType == DeletionType.SoftDelete)
            {
                foreach (var calculation in calculations)
                {
                    var calculationVersions = await calculationVersionRepo.GetManyAsQueryable(_ => _.CalculationId == calculation.CalculationId && !_.IsDeleted).ToListAsync();

                    foreach (var calculationVersion in calculationVersions)
                    {
                        calculationVersion.IsDeleted = true;
                        calculationVersion.UpdatedAt = DateTime.Now;
                    }
                    calculationVersionRepo.BulkUpdate(calculationVersions);

                    calculation.IsDeleted = true;
                }

                calculationRepo.BulkUpdate(calculations);
                
            }
            else
            {
                foreach (var calculation in calculations)
                {
                    calculationVersionRepo.Delete(_ => _.CalculationId == calculation.CalculationId);
                }
                calculations.ForEach(_ => calculationRepo.Delete(_));  
            }
        }

        public async Task<HttpStatusCode> DeleteObsoleteItem(string obsoleteItemId, string etag = null)
        {
            Guard.ArgumentNotNull(obsoleteItemId, nameof(obsoleteItemId));

            var obsoleteItemRepo = _uow.GenericRepository<EntityModel.ObsoleteItem>();
            var obsoleteItemCalcsRepo = _uow.GenericRepository<EntityModel.ObsoleteItemCalc>();

            var obsoleteItem = obsoleteItemRepo.GetFirstAsQueryable(_ => _.ObsoleteItemId == obsoleteItemId);

            var obsoleteItemCalcs = await obsoleteItemCalcsRepo.GetManyAsQueryable(_ => _.ObsoleteItemId == obsoleteItemId).ToListAsync();

            obsoleteItemRepo.Delete(obsoleteItem);
            obsoleteItemCalcs.ForEach(_ => obsoleteItemCalcsRepo.Delete(_));

            await _uow.CommitAsync();

            return HttpStatusCode.NoContent;
        }

        public async Task DeleteTemplateMappingsBySpecificationId(string specificationId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(deletionType, nameof(deletionType));

            var templateMappingRepo = _uow.GenericRepository<EntityModel.TemplateMapping>();
            var templateMappingItemsRepo = _uow.GenericRepository<EntityModel.TemplateMappingItem>();

            var templateMappings = await templateMappingRepo.GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();

            if (deletionType == DeletionType.SoftDelete)
            {
                foreach(var templateMapping in templateMappings)
                {
                    templateMapping.UpdatedAt = DateTime.Now;
                    templateMapping.IsDeleted = true;   
                }
                templateMappingRepo.BulkUpdate(templateMappings);
            }
            else
            {
                foreach(var templateMapping in templateMappings)
                {
                    var templateMappingItems = await templateMappingItemsRepo.GetManyAsQueryable(_ => _.TemplateMappingId == templateMapping.TemplateMappingId).ToListAsync();
                    templateMappingItems.ForEach(_ => templateMappingItemsRepo.Delete(_));
                }

                templateMappings.ForEach(_ => templateMappingRepo.Delete(_));
            }

            await _uow.CommitAsync();
        }

        public async Task<IEnumerable<Calculation>> GetAllCalculations()
        {
            List<Calculation> allCalculations = new List<Calculation>();

            var calculations = await _uow.GenericRepository<EntityModel.Calculation>().GetManyAsQueryable(_ => !_.IsDeleted).ToListAsync();

            if(!calculations.Any())
            {
                return Enumerable.Empty<Calculation>();
            }

            var calculationVersions = await _uow.GenericRepository<EntityModel.CalculationVersion>().GetManyAsQueryable(_ => _.IsLatest && !_.IsDeleted
            && calculations.Select(_ => _.CalculationId).Contains(_.CalculationId)).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
              .GetManyAsQueryable(_ => calculations.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            return await BuildAllCalculations(calculations, calculationVersions, fundingStreams);
        }

        private async Task<IEnumerable<Calculation>> BuildAllCalculations(List<EntityModel.Calculation> calculations, List<EntityModel.CalculationVersion> calculationVersions, List<EntityModel.FundingStream> fundingStreams)
        {
            return from calsVersion in calculationVersions
                   join cals in calculations
                   on calsVersion.CalculationId equals cals.CalculationId
                   join fundingStream in fundingStreams
                   on cals.FundingStreamId equals fundingStream.FundingStreamId
                   select new Calculation()
                   {
                       Id = cals.CalculationId,
                       SpecificationId = cals.SpecificationId,
                       FundingStreamId = fundingStream.FundingStreamCode,
                       Current = new CalculationVersion()
                       {
                           CalculationId = cals?.CalculationId,
                           SourceCode = calsVersion?.SourceCode,
                           CalculationType = Enum.Parse<CalculationType>(calsVersion?.CalculationType),
                           PublishStatus = Enum.Parse<PublishStatus>(calsVersion?.PublishStatus),
                           SourceCodeName = calsVersion?.SourceCodeName,
                           Name = calsVersion?.Name,
                           Date = calsVersion.Date,
                           Version = calsVersion.Version,
                           Namespace = Enum.Parse<CalculationNamespace>(calsVersion?.Namespace),
                           WasTemplateCalculation = calsVersion.WasTemplateCalculation,
                           ValueType = Enum.Parse<CalculationValueType>(calsVersion?.ValueType),
                           Description = calsVersion?.Description,
                           Author = new Reference(calsVersion?.AuthorId, calsVersion?.AuthorName),
                           DataType = calsVersion?.DataType != null ? Enum.Parse<CalculationDataType>(calsVersion?.DataType) : CalculationDataType.Decimal, //bydefault CalculationDataType.Decimal
                           AllowedEnumTypeValues = calsVersion.AllowedTypeValues != null
                   ? JsonConvert.DeserializeObject<IEnumerable<string>>(calsVersion?.AllowedTypeValues)
                   : null
                       }
                   };
        }

        public async Task<Calculation> GetCalculationById(string calculationId)
        {
            Guard.ArgumentNotNull(calculationId, nameof(calculationId));

            var calculation = await _uow.GenericRepository<EntityModel.Calculation>().SingleOrDefaultAsync(_ => _.CalculationId == calculationId && !_.IsDeleted);

            var calculationVersion = await _uow.GenericRepository<EntityModel.CalculationVersion>().SingleOrDefaultAsync(_ => _.CalculationId == calculationId && _.IsLatest && !_.IsDeleted);
            
            var fundingStreamCode = await GetFundingStream(calculation.FundingStreamId);

            return BuildCalculation(calculation, calculationVersion, fundingStreamCode);
        }

        public async Task<Calculation> GetCalculationBySpecificationIdAndCalculationName(string specificationId, string calculationName)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(calculationName, nameof(calculationName));

            var calculations = await _uow.GenericRepository<EntityModel.Calculation>().GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();
            
            if(!calculations.Any()) return null;

            var calculationVersion = await _uow.GenericRepository<EntityModel.CalculationVersion>()
                    .SingleOrDefaultAsync(_ => calculations.Select(_ => _.CalculationId).Contains(_.CalculationId) && _.Name == calculationName && _.IsLatest && !_.IsDeleted);
            
            if (calculationVersion == null) return null;

            var calculation = calculations.Where(_ => _.CalculationId == calculationVersion.CalculationId).FirstOrDefault();
            
            var fundingStreamCode = await GetFundingStream(calculation.FundingStreamId);

            return BuildCalculation(calculation, calculationVersion, fundingStreamCode); 
        }

        public async Task<Calculation> GetCalculationBySpecificationIdAndCalculationSourceCodeName(string specificationId, string calculationSourceCodeName)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(calculationSourceCodeName, nameof(calculationSourceCodeName));

            var calculations = await _uow.GenericRepository<EntityModel.Calculation>().GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();

            if (!calculations.Any()) return null;

            var calculationVersion = await _uow.GenericRepository<EntityModel.CalculationVersion>()
                    .SingleOrDefaultAsync(_ => calculations.Select(_ => _.CalculationId).Contains(_.CalculationId) && _.SourceCodeName == calculationSourceCodeName && _.IsLatest && !_.IsDeleted);

            if (calculationVersion == null) return null;

            var calculation = calculations.Where(_ => _.CalculationId == calculationVersion.CalculationId).FirstOrDefault();
            
            var fundingStreamCode = await GetFundingStream(calculation.FundingStreamId);

            return BuildCalculation(calculation, calculationVersion, fundingStreamCode);
        }

        public async Task<IEnumerable<Calculation>> GetCalculationsBySpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            List<Calculation> calculationsResult = new List<Calculation>();

            var calculations = await _uow.GenericRepository<EntityModel.Calculation>()
                .GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();

            if (calculations.Count == 0)
            {
                return [];
            }

            var calculationVersions = await _uow.GenericRepository<EntityModel.CalculationVersion>()
                .GetManyAsQueryable(_ => calculations.Select(_ => _.CalculationId).Contains(_.CalculationId) && _.IsLatest && !_.IsDeleted).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
             .GetManyAsQueryable(_ => calculations.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            return await BuildAllCalculations(calculations, calculationVersions, fundingStreams);

        }

        public async Task<IEnumerable<CalculationMetadata>> GetCalculationsMetatadataBySpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            List<CalculationMetadata> calculationsMetaDataList = new List<CalculationMetadata>();

            var calculations = await _uow.GenericRepository<EntityModel.Calculation>()
                .GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();

            if (calculations.Count == 0) return null;

            var calculationVersions = await _uow.GenericRepository<EntityModel.CalculationVersion>()
                .GetManyAsQueryable(_ => calculations.Select(_ => _.CalculationId).Contains(_.CalculationId) && _.IsLatest && !_.IsDeleted).ToListAsync();

            foreach (var calculation in calculations)
            {
                var calculationVersion = calculationVersions.Where(_ => _.CalculationId == calculation.CalculationId).FirstOrDefault();

                if (calculationVersion == null) continue;
                var fundingStreamCode = await GetFundingStream(calculation.FundingStreamId);
                calculationsMetaDataList.Add(new CalculationMetadata()
                {
                    CalculationId = calculation.CalculationId,
                    CalculationType = Enum.Parse<CalculationType>(calculationVersion.CalculationType),
                    SourceCodeName = calculationVersion.SourceCodeName,
                    Name = calculationVersion.Name,
                    Namespace = Enum.Parse<CalculationNamespace>(calculationVersion.Namespace),
                    WasTemplateCalculation = calculationVersion.WasTemplateCalculation,
                    ValueType = Enum.Parse<CalculationValueType>(calculationVersion.ValueType),
                    Description = calculationVersion.Description,
                    SpecificationId = calculation.SpecificationId,
                    FundingStreamId = fundingStreamCode,
                    PublishStatus = Enum.Parse<Models.Versioning.PublishStatus>(calculationVersion.PublishStatus)
                });
            }

            return calculationsMetaDataList;
        }

        public async Task<CompilerOptions> GetCompilerOptions(string specificationId)
        {
            return new CompilerOptions();
        }

        public async Task<int> GetCountOfNonApprovedTemplateCalculations(string specificationId)
        {
            Guard.IsNullOrWhiteSpace(specificationId, nameof(specificationId));

            var calculationIds = await _uow.GenericRepository<EntityModel.Calculation>().GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).Select(_ => _.CalculationId).ToListAsync();

            return await _uow.GenericRepository<EntityModel.CalculationVersion>()
                .GetManyAsQueryable(_ => calculationIds.Contains(_.CalculationId) 
                && _.CalculationType == CalculationType.Template.ToString()
                && _.PublishStatus != PublishStatus.Approved.ToString()
                && _.IsLatest && !_.IsDeleted)
                .CountAsync();
        }

        public async Task<ObsoleteItem> GetObsoleteItemById(string obsoleteItemId)
        {
            Guard.ArgumentNotNull(obsoleteItemId, nameof(obsoleteItemId));

            var obsoleteItem = _uow.GenericRepository<EntityModel.ObsoleteItem>().GetFirstAsQueryable(_ => _.ObsoleteItemId == obsoleteItemId && !_.IsDeleted);

            if(obsoleteItem == null) return null;

            var obsoleteItemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>().GetManyAsQueryable(_ => _.ObsoleteItemId == obsoleteItemId).ToListAsync();

            return BuildObsoleteItem(obsoleteItem, obsoleteItemCalcs);
        }

        public async Task<IEnumerable<DocumentEntity<ObsoleteItem>>> GetObsoleteItemDocumentsForCalculation(string calculationId, ObsoleteItemType obsoleteItemType)
        {
            Guard.ArgumentNotNull(calculationId, nameof(calculationId));
            Guard.ArgumentNotNull(obsoleteItemType, nameof(obsoleteItemType));

            List<DocumentEntity<ObsoleteItem>> obsoleteItemsDocuments = new List<DocumentEntity<ObsoleteItem>>();

            var obsoleteItemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>().GetManyAsQueryable(_ => _.CalculationId == calculationId).ToListAsync();

            if (obsoleteItemCalcs.Count == 0)
            {
                return [];
            }

            var obsoleteItems = await _uow.GenericRepository<EntityModel.ObsoleteItem>()
                .GetManyAsQueryable(_ => obsoleteItemCalcs.Select(_ => _.ObsoleteItemId).Contains(_.ObsoleteItemId)  && _.ItemType == obsoleteItemType.ToString() && !_.IsDeleted).ToListAsync();

            foreach (var obsoleteItem in obsoleteItems)
            {
                var itemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>()
                    .GetManyAsQueryable(_ => _.ObsoleteItemId == obsoleteItem.ObsoleteItemId).ToListAsync();

                obsoleteItemsDocuments.Add(new DocumentEntity<ObsoleteItem>()
                {
                    DocumentType = nameof(ObsoleteItem),
                    Content = BuildObsoleteItem(obsoleteItem, itemCalcs),
                    CreatedAt = obsoleteItem.CreatedAt,
                    UpdatedAt = obsoleteItem.UpdatedAt,
                    Deleted = obsoleteItem.IsDeleted
                });
            }

            return obsoleteItemsDocuments;
        }

        public async Task<IEnumerable<ObsoleteItem>> GetObsoleteItemsForCalculation(string calculationId)
        {
            Guard.ArgumentNotNull(calculationId, nameof(calculationId));

            List<ObsoleteItem> obsoleteItemsResult = new List<ObsoleteItem>();

            var obsoleteItemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>().GetManyAsQueryable(_ => _.CalculationId == calculationId).ToListAsync(); 

            if (obsoleteItemCalcs.Count == 0)
            {
                return [];
            }
 
            var obsoleteItems = await _uow.GenericRepository<EntityModel.ObsoleteItem>()
                .GetManyAsQueryable(_ => obsoleteItemCalcs.Select(_ => _.ObsoleteItemId).Contains(_.ObsoleteItemId)  && !_.IsDeleted).ToListAsync();

            foreach(var obsoleteItem in obsoleteItems)
            {
                var itemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>()
                    .GetManyAsQueryable(_ => _.ObsoleteItemId == obsoleteItem.ObsoleteItemId).ToListAsync();
                
                obsoleteItemsResult.Add(BuildObsoleteItem(obsoleteItem, itemCalcs));
            }
            return obsoleteItemsResult;
        }

        public async Task<IEnumerable<ObsoleteItem>> GetObsoleteItemsForCalculation(string calculationId, ObsoleteItemType obsoleteItemType)
        {
            Guard.ArgumentNotNull(calculationId, nameof(calculationId));
            Guard.ArgumentNotNull(obsoleteItemType, nameof(obsoleteItemType));

            List<ObsoleteItem> obsoleteItemsResult = new List<ObsoleteItem>();

            var obsoleteItemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>().GetManyAsQueryable(_ => _.CalculationId == calculationId).ToListAsync();

            if (obsoleteItemCalcs.Count == 0)
            {
                return [];
            }

            var obsoleteItems = await _uow.GenericRepository<EntityModel.ObsoleteItem>()
                .GetManyAsQueryable(_ => obsoleteItemCalcs.Select(_ => _.ObsoleteItemId).Contains(_.ObsoleteItemId) && _.ItemType == obsoleteItemType.ToString() && !_.IsDeleted).ToListAsync();

            foreach (var obsoleteItem in obsoleteItems)
            {
                var itemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>()
                    .GetManyAsQueryable(_ => _.ObsoleteItemId == obsoleteItem.ObsoleteItemId).ToListAsync();

                obsoleteItemsResult.Add(BuildObsoleteItem(obsoleteItem, itemCalcs));
            }
            return obsoleteItemsResult;
        }

        public async Task<IEnumerable<ObsoleteItem>> GetObsoleteItemsForSpecification(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            List<ObsoleteItem> obsoleteItemsResult = new List<ObsoleteItem>();

            var obsoleteItems = await _uow.GenericRepository<EntityModel.ObsoleteItem>()
                .GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).ToListAsync();

            if (obsoleteItems.Count == 0)
            {
                return [];
            }

            var obsoleteItemCalcs = await _uow.GenericRepository<EntityModel.ObsoleteItemCalc>()
                .GetManyAsQueryable(_ => obsoleteItems.Select(_ => _.ObsoleteItemId).Contains(_.ObsoleteItemId)).ToListAsync();

            foreach (var obsoleteItem in obsoleteItems)
            {
                var itemCalcs = obsoleteItemCalcs.Where(_ => _.ObsoleteItemId == obsoleteItem.ObsoleteItemId);

                obsoleteItemsResult.Add(BuildObsoleteItem(obsoleteItem, itemCalcs));
                
            }
            return obsoleteItemsResult;
        }

        public async Task<StatusCounts> GetStatusCounts(string specificationId)
        {
            Guard.IsNullOrWhiteSpace(specificationId, nameof(specificationId));

            var calculationIds = await _uow.GenericRepository<EntityModel.Calculation>()
                .GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).Select(_ => _.CalculationId).ToListAsync();

            return new StatusCounts()
            {
                Approved = GetCalculationsCountBasedOnStatus(calculationIds, PublishStatus.Approved).Result,
                Updated = GetCalculationsCountBasedOnStatus(calculationIds, PublishStatus.Updated).Result,
                Draft = GetCalculationsCountBasedOnStatus(calculationIds, PublishStatus.Draft).Result,
            };
        }

        private async Task<int> GetCalculationsCountBasedOnStatus(List<String> calculationIds, PublishStatus publishStatus)
        {
            return await _uow.GenericRepository<EntityModel.CalculationVersion>()
                .GetManyAsQueryable(_ => calculationIds.Contains(_.CalculationId)
                && _.CalculationType == CalculationType.Template.ToString()
                && _.PublishStatus == publishStatus.ToString()
                && _.IsLatest && !_.IsDeleted)
                .CountAsync();
        }

        public async Task<IEnumerable<Calculation>> GetTemplateCalculationsBySpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            List<Calculation> templateCalculations = new List<Calculation>();

            var calculations = await _uow.GenericRepository<EntityModel.Calculation>()
                .GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted)
                .ToListAsync();

            List<EntityModel.CalculationVersion> calculationVersions = await _uow.GenericRepository<EntityModel.CalculationVersion>()
                   .GetManyAsQueryable(_ => calculations.Select(_ => _.CalculationId).Contains(_.CalculationId)
               && _.CalculationType == CalculationType.Template.ToString()
               && _.IsLatest && !_.IsDeleted).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
             .GetManyAsQueryable(_ => calculations.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            return await BuildAllCalculations(calculations, calculationVersions, fundingStreams);        
        }

        public async Task<TemplateMapping> GetTemplateMapping(string specificationId, string fundingStreamId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(fundingStreamId, nameof(fundingStreamId));

            string templateMappingId = $"templatemapping-{specificationId}-{fundingStreamId}";

            var templateMapping = await _uow.GenericRepository<EntityModel.TemplateMapping>().FirstOrDefaultAsync(_ => _.TemplateMappingId == templateMappingId && !_.IsDeleted);

            var templateMappingItems = await _uow.GenericRepository<EntityModel.TemplateMappingItem>().GetManyAsQueryable(_ => _.TemplateMappingId == templateMappingId).ToListAsync();

            return new TemplateMapping()
            {
                SpecificationId = specificationId,
                FundingStreamId = fundingStreamId,
                TemplateMappingItems = templateMappingItems.Select(_ => new TemplateMappingItem()
                {
                    EntityType = Enum.Parse<TemplateMappingEntityType>(Enum.GetName(typeof(TemplateMappingEntityType), _.EntityType)),
                    Name = _.Name,
                    TemplateId = (uint)_.TemplateId,
                    CalculationId = _.CalculationId
                })
                .ToList()
            };
        }


        //Not Used in SQL Flow
        public async Task<HttpStatusCode> UpdateCalculation(Calculation calculation)
        {
            Guard.ArgumentNotNull(calculation, nameof(calculation));

            var calculationRepo = _uow.GenericRepository<EntityModel.Calculation>();

            var existingcals = await calculationRepo.SingleOrDefaultAsync(_ => _.CalculationId == calculation.Id);
            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>()
                .SingleOrDefaultAsync(_ => _.FundingStreamCode == calculation.FundingStreamId);

            if (existingcals != null)
            {
                existingcals.SpecificationId = calculation.SpecificationId;
                existingcals.FundingStreamId = fundingStream.FundingStreamId;
                existingcals.IsDeleted = false;

                calculationRepo.Update(existingcals);
            }

            return HttpStatusCode.OK;
        }

        public async Task UpdateCalculations(IEnumerable<Calculation> calculations)
        {
            Guard.ArgumentNotNull(calculations, nameof(calculations));

            var calculationVersionRepo = _uow.GenericRepository<EntityModel.CalculationVersion>();

            var calculationVersions = await calculationVersionRepo
                .GetManyAsQueryable(_ => calculations.Select(_ => _.Id).Contains(_.CalculationId) && _.IsLatest && !_.IsDeleted).ToListAsync();

            foreach (var calculationversion in calculationVersions)
            {
                var calculation = calculations.Where(_ => _.Id == calculationversion.CalculationId).FirstOrDefault();

                calculationversion.PublishStatus = calculation.Current.PublishStatus.ToString();
                calculationversion.Date = DateTime.Now;
                calculationversion.UpdatedAt = DateTime.Now;
            }

            calculationVersionRepo.BulkUpdate(calculationVersions);

            await _uow.CommitAsync();
        }

        public async Task<HttpStatusCode> UpdateObsoleteItem(ObsoleteItem obsoleteItem, string etag = null)
        {
            Guard.ArgumentNotNull(obsoleteItem, nameof(obsoleteItem));

            var obsoleteItemRepo = _uow.GenericRepository<EntityModel.ObsoleteItem>();
            var obsoleteItemCalcsRepo = _uow.GenericRepository<EntityModel.ObsoleteItemCalc>();

            var exisitngObsoleteItem = obsoleteItemRepo.GetFirstAsQueryable(_ => _.ObsoleteItemId == obsoleteItem.Id && !_.IsDeleted);

            if (exisitngObsoleteItem == null)
            {

                var fundingStreamId = _uow.GenericRepository<EntityModel.FundingStream>()
                    .GetFirstAsQueryable(_ => _.FundingStreamCode == obsoleteItem.FundingStreamId).FundingStreamId;

                EntityModel.ObsoleteItem newObsoleteItem = new EntityModel.ObsoleteItem()
                {
                    ObsoleteItemId = obsoleteItem.Id,
                    SpecificationId = obsoleteItem.SpecificationId,
                    DatasetRelationshipId = obsoleteItem.DatasetRelationshipId,
                    DatasetRelationshipName = obsoleteItem.DatasetRelationshipName,
                    DatasetFieldId = obsoleteItem.DatasetFieldId,
                    DatasetFieldName = obsoleteItem.DatasetFieldName,
                    DatasetDataType = obsoleteItem.DatasetDatatype.ToString(),
                    IsReleasedData = obsoleteItem.IsReleasedData,
                    RelationshipName = obsoleteItem.RelationshipName,
                    ItemType = obsoleteItem.ItemType.ToString(),
                    EnumValueName = obsoleteItem.EnumValueName,
                    FundingLineId = (int?)obsoleteItem.FundingLineId,
                    FundingStreamId = fundingStreamId,
                    TemplateCalculationId = (int?)obsoleteItem.TemplateCalculationId,
                    CodeReference = obsoleteItem.CodeReference,
                    FundingLineName = obsoleteItem?.FundingLineName,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };

                List<EntityModel.ObsoleteItemCalc> newObsoleteItemCalcs = obsoleteItem.CalculationIds.Select(_ => new EntityModel.ObsoleteItemCalc()
                {
                    CalculationId = _,
                    ObsoleteItemId = obsoleteItem.Id
                }).ToList();

                obsoleteItemRepo.Insert(newObsoleteItem);
                obsoleteItemCalcsRepo.BulkInsertAsync(newObsoleteItemCalcs);
            }
            else
            {
                var existingObsoleteItemCalcs = await obsoleteItemCalcsRepo.GetManyAsQueryable(_ => _.ObsoleteItemId == obsoleteItem.Id).ToListAsync();
                existingObsoleteItemCalcs.ForEach(_ => obsoleteItemCalcsRepo.Delete(_));

                if (obsoleteItem.CalculationIds.Any())
                {
                    List<EntityModel.ObsoleteItemCalc> newObsoleteItemCalcs = obsoleteItem.CalculationIds.Select(_ => new EntityModel.ObsoleteItemCalc()
                    {
                        CalculationId = _,
                        ObsoleteItemId = obsoleteItem.Id
                    }).ToList();

                    obsoleteItemCalcsRepo.BulkInsertAsync(newObsoleteItemCalcs);
                }
            }

            await _uow.CommitAsync();

            return HttpStatusCode.OK;
        }

        public async Task UpdateTemplateMapping(string specificationId, string fundingStreamId, TemplateMapping templateMapping)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(fundingStreamId, nameof(fundingStreamId));

            string templateMappingId = $"templatemapping-{specificationId}-{fundingStreamId}";

            var templateMappingRepo = _uow.GenericRepository<EntityModel.TemplateMapping>();
            var templateMappingItemsRepo = _uow.GenericRepository<EntityModel.TemplateMappingItem>();

            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(_ => _.FundingStreamCode == templateMapping.FundingStreamId);
            
            var templateMappingData = new EntityModel.TemplateMapping()
            {

                TemplateMappingId = templateMapping.Id,
                SpecificationId = specificationId,
                FundingStreamId = fundingStream.FundingStreamId,               
                IsDeleted = false
            };
            await templateMappingRepo.Upsert(templateMappingData, _ => _.TemplateMappingId == templateMappingId && !_.IsDeleted);

            var existingtemplateMappingItem = await templateMappingItemsRepo.GetManyAsQueryable(_ => _.TemplateMappingId == templateMappingId).ToListAsync();
            
            if (!existingtemplateMappingItem.Any())
            {
                List<EntityModel.TemplateMappingItem> templateMappingItems = [.. templateMapping.TemplateMappingItems.Select(_ =>
                new EntityModel.TemplateMappingItem()
                {
                    TemplateMappingId = templateMappingId,
                    Name = _.Name,
                    EntityType = (int)_.EntityType,
                    CalculationId = _.CalculationId,
                    TemplateId = (int)_.TemplateId
                })];

                templateMappingItemsRepo.BulkInsertAsync(templateMappingItems);
            }
            else
            {
                existingtemplateMappingItem.ForEach(_ => templateMappingItemsRepo.Delete(_));
                await _uow.CommitAsync();
                List<EntityModel.TemplateMappingItem> templateMappingItems = [.. templateMapping.TemplateMappingItems.Select(_ =>
                                                                                new EntityModel.TemplateMappingItem()
                                                                                {
                                                                                    TemplateMappingId = templateMappingId,
                                                                                    Name = _.Name,
                                                                                    EntityType = (int)_.EntityType,
                                                                                    CalculationId = _.CalculationId,
                                                                                    TemplateId = (int)_.TemplateId
                                                                                })];

                templateMappingItemsRepo.BulkInsertAsync(templateMappingItems);
            }
            await _uow.CommitAsync();
        }
        private async Task<string> GetFundingStream(int fundingStreamId)
        {
            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>().SingleOrDefaultAsync(x => x.FundingStreamId == fundingStreamId);

            return fundingStream.FundingStreamCode;
        }
       
        private ObsoleteItem BuildObsoleteItem(EntityModel.ObsoleteItem obsoleteItem, IEnumerable<EntityModel.ObsoleteItemCalc> obsoleteItemCalcs)
        {
            var fundingStreamCode =  GetFundingStream(obsoleteItem.FundingStreamId).Result;
            return new ObsoleteItem()
            {
                Id = obsoleteItem.ObsoleteItemId,
                SpecificationId = obsoleteItem.SpecificationId,
                DatasetRelationshipId = obsoleteItem.DatasetRelationshipId,
                DatasetRelationshipName = obsoleteItem.DatasetRelationshipName,
                DatasetFieldId = obsoleteItem.DatasetFieldId,
                DatasetFieldName = obsoleteItem.DatasetFieldName,
                DatasetDatatype = Enum.Parse<DatasetFieldType>(obsoleteItem.DatasetDataType),
                IsReleasedData = (bool)obsoleteItem.IsReleasedData,
                RelationshipName = obsoleteItem.RelationshipName,
                ItemType = Enum.Parse<ObsoleteItemType>(obsoleteItem.ItemType),
                EnumValueName = obsoleteItem.EnumValueName,
                FundingLineId = (uint?)obsoleteItem.FundingLineId,
                FundingStreamId = fundingStreamCode,
                TemplateCalculationId = (uint?)obsoleteItem.TemplateCalculationId,
                CodeReference = obsoleteItem.CodeReference,
                CalculationIds = obsoleteItemCalcs != null ? obsoleteItemCalcs.Select(_ => _.CalculationId).ToList() : null,
                FundingLineName = obsoleteItem.FundingLineName,
            };
        }

        private Calculation BuildCalculation(EntityModel.Calculation calculation, EntityModel.CalculationVersion calculationVersion, string fundingStreamCode)
        {           
                var result = new Calculation()
                {
                    Id = calculation.CalculationId,
                    SpecificationId = calculation.SpecificationId,
                    FundingStreamId = fundingStreamCode,
                    Current = new CalculationVersion()
                    {
                        CalculationId = calculation?.CalculationId,
                        SourceCode = calculationVersion?.SourceCode,
                        CalculationType = Enum.Parse<CalculationType>(calculationVersion?.CalculationType),
                        PublishStatus = Enum.Parse<PublishStatus>(calculationVersion?.PublishStatus),
                        SourceCodeName = calculationVersion?.SourceCodeName,
                        Name = calculationVersion?.Name,
                        Version = calculationVersion.Version,
                        Date = calculationVersion.Date,
                        Namespace = Enum.Parse<CalculationNamespace>(calculationVersion?.Namespace),
                        WasTemplateCalculation = calculationVersion.WasTemplateCalculation,
                        ValueType = Enum.Parse<CalculationValueType>(calculationVersion?.ValueType),
                        Description = calculationVersion?.Description,
                        Author = new Reference(calculationVersion.AuthorId, calculationVersion.AuthorName),
                        DataType = calculationVersion?.DataType != null ? Enum.Parse<CalculationDataType>(calculationVersion?.DataType) : CalculationDataType.Decimal, //bydefault CalculationDataType.Decimal
                        AllowedEnumTypeValues = calculationVersion.AllowedTypeValues != null
                   ? JsonConvert.DeserializeObject<IEnumerable<string>>(calculationVersion?.AllowedTypeValues)
                   : null
                    }
                };
                return result;         
        }
    }
}
