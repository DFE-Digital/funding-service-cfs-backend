using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Models.Versioning;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Messages;
using CalculateFunding.Models.Providers;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Specs.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Specification = CalculateFunding.Models.Specs.Specification;

namespace CalculateFunding.Services.Specs
{
    public class SpecificationsRepository : ISpecificationsRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;

        public SpecificationsRepository(IUnitOfWork uow)
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

        
        public async Task<DocumentEntity<Specification>> CreateSpecification(Specification specification)
        {
            Guard.ArgumentNotNull(specification, nameof(specification));

            var specRepo = _uow.GenericRepository<EntityModel.Specification>();

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirst(_ => _.FundingStreamCode == specification.Current.FundingStreams.First().Id);

            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetFirst(_ => _.FundingPeriodCode == specification.Current.FundingPeriod.Id);

            var existingSpecification = new EntityModel.Specification()
            {
                SpecificationId = specification.Id,
                ForceUpdateOnNextRefresh = specification.ForceUpdateOnNextRefresh != null,
                IsSelectedForFunding = specification.IsSelectedForFunding,
                FundingPeriodId = fundingPeriod.FundingPeriodId,
                FundingStreamId = fundingStream.FundingStreamId,
                SpecificationName = specification.Name,
                IsDeleted = false,
            };
            specRepo.Insert(existingSpecification);

            return new DocumentEntity<Specification>() 
            { 
                Content = specification
            };

        }

        public async Task DeleteSpecifications(string specificationId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var specRepo = _uow.GenericRepository<EntityModel.Specification>();
            var specVersionRepo = _uow.GenericRepository<EntityModel.SpecificationVersion>();

            if (deletionType == DeletionType.SoftDelete)
            {
                var existingSpecification = specRepo.GetFirstAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted);
              
                if (existingSpecification != null)
                {
                    var existingSpecificationVersions = specVersionRepo.GetManyAsQueryable(_ => _.SpecificationId == specificationId
                                                                                                              && !_.IsDeleted);
                    foreach (var specVersion in existingSpecificationVersions)
                    {
                        specVersion.IsDeleted = true;
                        specVersion.UpdatedAt = DateTime.UtcNow;
                        specVersionRepo.Update(specVersion);
                    }

                    existingSpecification.IsDeleted = true;
                    specRepo.Update(existingSpecification);

                    await _uow.CommitAsync();
                }

            }
            else if (deletionType == DeletionType.PermanentDelete)
            {
                var specification = specRepo.GetFirstAsQueryable(_ => _.SpecificationId == specificationId);

                var vpRepo = _uow.GenericRepository<EntityModel.VariationPointer>();
                var specificationRelationshipRepo = _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>();

                var specificationVersions = specVersionRepo.GetManyAsQueryable(_ => _.SpecificationId == specification.SpecificationId);


                var definitionSpecificationRelationships = specificationRelationshipRepo.GetManyAsQueryable(_ => specificationVersions
                                                                                         .Select(v => v.SpecificationVersionId)
                                                                                         .Contains(_.SpecificationVersionId)).ToList();
                if (definitionSpecificationRelationships.Any())
                {
                    specificationRelationshipRepo.Delete(_ => specificationVersions.Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId));
                }

                var variationPointers = vpRepo.GetManyAsQueryable(_ => specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToList();
                
                if (variationPointers.Any())
                {
                    vpRepo.Delete(_ => specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId));
                }

                if (specificationVersions.Any())
                {
                    specVersionRepo.Delete(_ => _.SpecificationId == specification.SpecificationId);
                }
                if (specification != null)
                {
                    specRepo.Delete(specification);
                }
                await _uow.CommitAsync();
            }
        }

       
        public async Task<IEnumerable<Specification>> GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream(string fundingPeriodId, string fundingStreamId)
        {
            Guard.ArgumentNotNull(fundingPeriodId, nameof(fundingPeriodId));
            Guard.ArgumentNotNull(fundingStreamId, nameof(fundingStreamId));

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirst(_ => _.FundingStreamCode == fundingStreamId);

            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetFirst(_ => _.FundingPeriodCode == fundingPeriodId);

            var specifications = await _uow.GenericRepository<EntityModel.Specification>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.FundingStreamId == fundingStream.FundingStreamId && _.FundingPeriodId == fundingPeriod.FundingPeriodId)
                .ToListAsync();

            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && (_.PublishStatus == PublishStatus.Approved.ToString() || _.PublishStatus == PublishStatus.Updated.ToString()) && specifications.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions
                .Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();
            
            return BuildSpecificationModel(fundingStream, fundingPeriod, specifications, specificationVersions, definitionSpecificationRelationships, variationPointers);

        }


        
        public async Task<IEnumerable<string>> GetDistinctFundingStreamsForSpecifications()
        {
            var specifications = await _uow.GenericRepository<EntityModel.Specification>().GetManyAsQueryable(_ => !_.IsDeleted).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            return fundingStreams.Select(_ => _.FundingStreamName).ToList();
        }

        
        public async Task<IEnumerable<string>> GetDistinctProviderVersionIdsFromSpecifications(IEnumerable<string> specificationIds)
        {
           return await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && _.ProviderVersionId != null && specificationIds.Contains(_.SpecificationId)).Select(_ => _.ProviderVersionId).ToListAsync();

        }

        public async Task<Specification> GetSpecificationById(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var spec = await _uow.GenericRepository<EntityModel.Specification>().FirstOrDefaultAsync(_ => _.SpecificationId == specificationId
                                                                    && !_.IsDeleted);
            if(spec == null)
            {
                return null;
            }

            var specVersion = await _uow.GenericRepository<EntityModel.SpecificationVersion>().FirstOrDefaultAsync(_ => _.SpecificationId == specificationId
                                                                    && !_.IsDeleted && _.IsLatest);


            var fundingPeriod = await GetFundingPeriod(spec.FundingPeriodId);
            var fundingStream = await GetFundingStream(spec.FundingStreamId);
            var fundingStreamCode = fundingStream.Select(x => x.Id).Single();

            Dictionary<string, string> templateIds = new Dictionary<string, string>
            {
                { fundingStreamCode, Convert.ToString(specVersion.TemplateVersion) }
            };

            var dataDefinitionRelationshipId =  await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                                                   .GetManyAsQueryable(x => x.SpecificationVersionId == specVersion.SpecificationVersionId && !x.IsDeleted)
                                                   .Select(x => x.DataDefinitionRelationshipId).ToListAsync();

            var profileVariationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                                                                .GetManyAsQueryable(_ => !_.IsDeleted && _.SpecificationVersionId == specVersion.SpecificationVersionId)
                                                                .Select(x => new ProfileVariationPointer()
                                                                {
                                                                    FundingStreamId = fundingStreamCode,
                                                                    FundingLineId = x.FundingLineId,
                                                                    PeriodType = x.PeriodType,
                                                                    TypeValue = x.PeriodValue,
                                                                    Year = x.Year,
                                                                    Occurrence = x.Occurrence
                                                                }).ToListAsync();

            Specification Specification = new Specification
            {
                Id = spec.SpecificationId,
                ForceUpdateOnNextRefresh = spec.ForceUpdateOnNextRefresh,
                Name = spec.SpecificationName,
                IsSelectedForFunding = spec.IsSelectedForFunding,
                Current = new Models.Specs.SpecificationVersion()
                {
                    Name = specVersion.SpecificationName,
                    SpecificationId = spec.SpecificationId,
                    FundingPeriod = fundingPeriod,
                    ProviderVersionId = specVersion.ProviderVersionId,
                    FundingStreams = fundingStream,
                    Description = specVersion.Description,
                    DataDefinitionRelationshipIds = dataDefinitionRelationshipId,
                    TemplateIds = templateIds,
                    ExternalPublicationDate = specVersion.ExternalPublicationDate,
                    EarliestPaymentAvailableDate = specVersion.EarliestPaymentAvailableDate,
                    ProfileVariationPointers = profileVariationPointers,
                    ProviderSource = Enum.Parse<ProviderSource>(specVersion.ProviderSource),
                    ProviderSnapshotId = specVersion.ProviderSnapshotId,
                    CoreProviderVersionUpdates = Enum.Parse<CoreProviderVersionUpdates>(specVersion.CoreProviderVersionUpdates),
                    Version = specVersion.Version,
                    Date = specVersion.Date,
                    UpdatedAt = specVersion.UpdatedAt,
                    Author = new Reference
                    {
                        Id = specVersion.AuthorId,
                        Name = specVersion.AuthorName,
                    },
                    Comment = specVersion.Comment,
                    PublishStatus = Enum.Parse<Models.Versioning.PublishStatus>(specVersion.PublishStatus)
                }
            };

            return Specification;

        }

        public async Task<Specification> GetSpecificationByQuery(Expression<Func<DocumentEntity<Specification>, bool>> query)
        {
            throw new NotImplementedException();
        }

        public async Task<DocumentEntity<Specification>> GetSpecificationDocumentEntityById(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var result = await GetSpecificationById(specificationId);

            return new DocumentEntity<Specification>()
            {
                Content = result,
            };
        }

        
        public async Task<IEnumerable<Specification>> GetSpecifications()
        {
            var specifications = await _uow.GenericRepository<EntityModel.Specification>().GetManyAsQueryable(_=>!_.IsDeleted).ToListAsync();

            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && specifications.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();  

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specifications
                .Select(v=>v.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
                .GetManyAsQueryable(_=>specifications.Select(s=>s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            var fundingPeriods = await _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingPeriodId).Contains(_.FundingPeriodId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            return ((from specVersion in specificationVersions
                     join spec in specifications
                     on specVersion.SpecificationId equals spec.SpecificationId
                     join fundingStream in fundingStreams
                     on spec.FundingStreamId equals fundingStream.FundingStreamId
                     join fundingPeriod in fundingPeriods
                     on spec.FundingPeriodId equals fundingPeriod.FundingPeriodId
                     select new Specification()
                     {
                         Id = spec.SpecificationId,
                         ForceUpdateOnNextRefresh = spec.ForceUpdateOnNextRefresh,
                         Name = spec.SpecificationName,
                         IsSelectedForFunding = spec.IsSelectedForFunding,
                         Current = new Models.Specs.SpecificationVersion()
                         {
                             Name = specVersion.SpecificationName,
                             SpecificationId = spec.SpecificationId,
                             Author = new Reference
                             {
                                 Id = specVersion.AuthorId,
                                 Name = specVersion.AuthorName,
                             },
                             ExternalPublicationDate = specVersion.ExternalPublicationDate,
                             Description = specVersion.Description,
                             Comment = specVersion.Comment,
                             EarliestPaymentAvailableDate = specVersion.EarliestPaymentAvailableDate,
                             CoreProviderVersionUpdates = (CoreProviderVersionUpdates)Enum.Parse(typeof(CoreProviderVersionUpdates), specVersion.CoreProviderVersionUpdates),
                             Date = specVersion.Date,
                             ProviderSnapshotId = specVersion.ProviderSnapshotId,
                             ProviderSource = (ProviderSource)Enum.Parse(typeof(ProviderSource), specVersion.ProviderSource),
                             ProviderVersionId = specVersion.ProviderVersionId,
                             PublishStatus = (Models.Versioning.PublishStatus)Enum.Parse(typeof(PublishStatus), specVersion.PublishStatus),
                             Version = specVersion.Version,
                             TemplateIds = new Dictionary<string, string>() { { fundingStream.FundingStreamCode.ToString(), specVersion.TemplateVersion.ToString() } },
                             DataDefinitionRelationshipIds = definitionSpecificationRelationships.Where(_ => _.SpecificationId == specVersion.SpecificationId && !_.IsDeleted).Select(_ => _.DatasetSpecificationRelationshipId).ToList(),
                             UpdatedAt = specVersion.UpdatedAt,
                             FundingPeriod = new Reference()
                             {
                                 Id = fundingPeriod.FundingPeriodCode,
                                 Name = fundingPeriod.FundingPeriodName
                             },
                             FundingStreams = new List<Reference>()
                             {
                                 new Reference()
                                 {
                                     Id = fundingStream.FundingStreamCode,
                                     Name = fundingStream.FundingStreamName
                                 }
                             },
                             ProfileVariationPointers = variationPointers.Where(v=>v.SpecificationVersionId == specVersion.SpecificationVersionId)
                             .Select(x=>
                                 new ProfileVariationPointer()
                                 {
                                     FundingStreamId = fundingStream.FundingStreamCode,
                                     FundingLineId = x.FundingLineId,
                                     PeriodType = x.PeriodType,
                                     TypeValue = x.PeriodValue,
                                     Year = x.Year,
                                     Occurrence = x.Occurrence
                                 }
                             ).ToList(),
                         }
                     }));
        }

        
        public async Task<IEnumerable<Specification>> GetSpecificationsByFundingPeriodAndFundingStream(string fundingPeriodId, string fundingStreamId)
        {
            Guard.ArgumentNotNull(fundingPeriodId, nameof(fundingPeriodId));
            Guard.ArgumentNotNull(fundingStreamId, nameof(fundingStreamId));

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirst(_ => _.FundingStreamCode == fundingStreamId);

            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetFirst(_ => _.FundingPeriodCode == fundingPeriodId);

            var specifications = await _uow.GenericRepository<EntityModel.Specification>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.FundingStreamId == fundingStream.FundingStreamId && _.FundingPeriodId == fundingPeriod.FundingPeriodId)
                .ToListAsync();

            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && specifications.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions
                .Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            return BuildSpecificationModel(fundingStream, fundingPeriod, specifications, specificationVersions, definitionSpecificationRelationships, variationPointers);

        }

        public Task<IEnumerable<Specification>> GetSpecificationsByQuery(Expression<Func<DocumentEntity<Specification>, bool>> query = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetSpecificationsByRawQuery<T>(CosmosDbQuery cosmosDbQuery)
        {
            throw new NotImplementedException();
        }

        
        public async Task<IEnumerable<Specification>> GetSpecificationsSelectedForFundingByPeriod(string fundingPeriodId)
        {
            Guard.ArgumentNotNull(fundingPeriodId, nameof(fundingPeriodId));

            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetFirstAsQueryable(_ => _.FundingPeriodCode == fundingPeriodId);

            var specifications = await _uow.GenericRepository<EntityModel.Specification>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsSelectedForFunding && _.FundingPeriodId == fundingPeriod.FundingPeriodId)
                .ToListAsync();

            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && specifications.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions
                .Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();


            return ((from specVersion in specificationVersions
                     join spec in specifications
                     on specVersion.SpecificationId equals spec.SpecificationId
                     join fundingStream in fundingStreams
                     on spec.FundingStreamId equals fundingStream.FundingStreamId
                     select new Specification()
                     {
                         Id = spec.SpecificationId,
                         ForceUpdateOnNextRefresh = spec.ForceUpdateOnNextRefresh,
                         Name = spec.SpecificationName,
                         IsSelectedForFunding = spec.IsSelectedForFunding,
                         Current = new Models.Specs.SpecificationVersion()
                         {
                             Name = specVersion.SpecificationName,
                             SpecificationId = spec.SpecificationId,
                             Author = new Reference
                             {
                                 Id = specVersion.AuthorId,
                                 Name = specVersion.AuthorName,
                             },
                             ExternalPublicationDate = specVersion.ExternalPublicationDate,
                             Description = specVersion.Description,
                             Comment = specVersion.Comment,
                             EarliestPaymentAvailableDate = specVersion.EarliestPaymentAvailableDate,
                             CoreProviderVersionUpdates = (CoreProviderVersionUpdates)Enum.Parse(typeof(CoreProviderVersionUpdates), specVersion.CoreProviderVersionUpdates),
                             Date = specVersion.Date,
                             ProviderSnapshotId = specVersion.ProviderSnapshotId,
                             ProviderSource = (ProviderSource)Enum.Parse(typeof(ProviderSource), specVersion.ProviderSource),
                             ProviderVersionId = specVersion.ProviderVersionId,
                             PublishStatus = (Models.Versioning.PublishStatus)Enum.Parse(typeof(PublishStatus), specVersion.PublishStatus),
                             Version = specVersion.Version,
                             UpdatedAt = specVersion.UpdatedAt,
                             TemplateIds = new Dictionary<string, string>() { { fundingStream.FundingStreamCode.ToString(), specVersion.TemplateVersion.ToString() } },
                             DataDefinitionRelationshipIds = definitionSpecificationRelationships.Where(_ => _.SpecificationVersionId == specVersion.SpecificationVersionId && !_.IsDeleted).Select(_ => _.DataDefinitionRelationshipId).ToList(),
                             FundingPeriod = new Reference()
                             {
                                 Id = fundingPeriod.FundingPeriodCode,
                                 Name = fundingPeriod.FundingPeriodName
                             },
                             FundingStreams = new List<Reference>()
                             {
                                 new Reference()
                                 {
                                     Id = fundingStream.FundingStreamCode,
                                     Name = fundingStream.FundingStreamName
                                 }
                             },
                             ProfileVariationPointers = variationPointers.Where(v => v.SpecificationVersionId == specVersion.SpecificationVersionId)
                             .Select(x =>
                                 new ProfileVariationPointer()
                                 {
                                     FundingStreamId = fundingStream.FundingStreamCode,
                                     FundingLineId = x.FundingLineId,
                                     PeriodType = x.PeriodType,
                                     TypeValue = x.PeriodValue,
                                     Year = x.Year,
                                     Occurrence = x.Occurrence
                                 }
                             ).ToList(),
                         }
                     }));

        }

        
        public async Task<IEnumerable<Specification>> GetSpecificationsSelectedForFundingByPeriodAndFundingStream(string fundingPeriodId, string fundingStreamId)
        {
            Guard.ArgumentNotNull(fundingPeriodId, nameof(fundingPeriodId));
            Guard.ArgumentNotNull(fundingStreamId, nameof(fundingStreamId));

            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirst(_ => _.FundingStreamCode == fundingStreamId);

            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetFirst(_ => _.FundingPeriodCode == fundingPeriodId);

            var specifications = await _uow.GenericRepository<EntityModel.Specification>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsSelectedForFunding && _.FundingStreamId == fundingStream.FundingStreamId && _.FundingPeriodId == fundingPeriod.FundingPeriodId)
                .ToListAsync();

            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && specifications.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions
                .Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            return BuildSpecificationModel(fundingStream, fundingPeriod, specifications, specificationVersions, definitionSpecificationRelationships, variationPointers);
        }

        public async Task<HttpStatusCode> UpdateSpecification(Specification specification, bool allowCommit = false)
        {
            Guard.ArgumentNotNull(specification, nameof(specification));

            var specRepo = _uow.GenericRepository<EntityModel.Specification>();

            var existingSpecification = specRepo
                .GetFirst(_ => _.SpecificationId == specification.Id && !_.IsDeleted);

            if (existingSpecification != null)
            {
                existingSpecification.ForceUpdateOnNextRefresh = (bool)specification.ForceUpdateOnNextRefresh;
                existingSpecification.SpecificationName = specification.Name;
                existingSpecification.IsSelectedForFunding = specification.IsSelectedForFunding;
                specRepo.Update(existingSpecification);

                //Below Method is Use for update the flag for select funding ,deselect funding or ClearForceUpdateOnNextRefresh
                if (allowCommit)
                {
                    await _uow.CommitAsync();
                }
            }

            return HttpStatusCode.OK;
        }

        private async Task<Reference> GetFundingPeriod(int fundingPeriodId)
        {    
            var fundingPeriod = await _uow.GenericRepository<EntityModel.FundingPeriod>()
                                        .SingleOrDefaultAsync(x => x.FundingPeriodId == fundingPeriodId);
            Reference reference = new Reference();
            reference.Id = fundingPeriod.FundingPeriodCode;
            reference.Name = fundingPeriod.FundingPeriodName;
            return reference;
        }

        private async Task<IEnumerable<Reference>> GetFundingStream(int fundingStreamId)
        {
            var fundingStream = await _uow.GenericRepository<EntityModel.FundingStream>().GetManyAsQueryable(x => x.FundingStreamId == fundingStreamId)
                                                   .Select(x => new Reference
                                                   {
                                                       Id = x.FundingStreamCode,
                                                       Name = x.FundingStreamName
                                                   }).ToListAsync();
            return fundingStream;
        }


        private static IEnumerable<Specification> BuildSpecificationModel(EntityModel.FundingStream fundingStream, EntityModel.FundingPeriod fundingPeriod, List<EntityModel.Specification> specifications, List<EntityModel.SpecificationVersion> specificationVersions, List<EntityModel.DefinitionSpecificationRelationship> definitionSpecificationRelationships, List<EntityModel.VariationPointer> variationPointers)
        {
            return ((from specVersion in specificationVersions
                     join spec in specifications
                     on specVersion.SpecificationId equals spec.SpecificationId
                     select new Specification()
                     {
                         Id = spec.SpecificationId,
                         ForceUpdateOnNextRefresh = spec.ForceUpdateOnNextRefresh,
                         Name = spec.SpecificationName,
                         IsSelectedForFunding = spec.IsSelectedForFunding,
                         Current = new Models.Specs.SpecificationVersion()
                         {
                             Name = specVersion.SpecificationName,
                             SpecificationId = spec.SpecificationId,
                             Author = new Reference
                             {
                                 Id = specVersion.AuthorId,
                                 Name = specVersion.AuthorName,
                             },
                             ExternalPublicationDate = specVersion.ExternalPublicationDate,
                             Description = specVersion.Description,
                             Comment = specVersion.Comment,
                             EarliestPaymentAvailableDate = specVersion.EarliestPaymentAvailableDate,
                             CoreProviderVersionUpdates = (CoreProviderVersionUpdates)Enum.Parse(typeof(CoreProviderVersionUpdates), specVersion.CoreProviderVersionUpdates),
                             Date = specVersion.Date,
                             ProviderSnapshotId = specVersion.ProviderSnapshotId,
                             ProviderSource = (ProviderSource)Enum.Parse(typeof(ProviderSource), specVersion.ProviderSource),
                             ProviderVersionId = specVersion.ProviderVersionId,
                             PublishStatus = (Models.Versioning.PublishStatus)Enum.Parse(typeof(PublishStatus), specVersion.PublishStatus),
                             Version = specVersion.Version,
                             UpdatedAt = specVersion.UpdatedAt,
                             TemplateIds = new Dictionary<string, string>() { { fundingStream.FundingStreamCode.ToString(), specVersion.TemplateVersion.ToString() } },
                             DataDefinitionRelationshipIds = definitionSpecificationRelationships.Where(_ => _.SpecificationVersionId == specVersion.SpecificationVersionId && !_.IsDeleted).Select(_ => _.DataDefinitionRelationshipId).ToList(),
                             FundingPeriod = new Reference()
                             {
                                 Id = fundingPeriod.FundingPeriodCode,
                                 Name = fundingPeriod.FundingPeriodName
                             },
                             FundingStreams = new List<Reference>()
                             {
                                 new Reference()
                                 {
                                     Id = fundingStream.FundingStreamCode,
                                     Name = fundingStream.FundingStreamName
                                 }
                             },
                             ProfileVariationPointers = variationPointers.Where(v => v.SpecificationVersionId == specVersion.SpecificationVersionId)
                             .Select(x =>
                                 new ProfileVariationPointer()
                                 {
                                     FundingStreamId = fundingStream.FundingStreamCode,
                                     FundingLineId = x.FundingLineId,
                                     PeriodType = x.PeriodType,
                                     TypeValue = x.PeriodValue,
                                     Year = x.Year,
                                     Occurrence = x.Occurrence
                                 }
                             ).ToList(),
                         }
                     }));
        }

        public async Task<IEnumerable<Specification>> GetSpecificationsBySQLQuery(Expression<Func<EntityModel.Specification, bool>> query)
        {

            var specifications = await _uow.GenericRepository<EntityModel.Specification>().GetManyAsQueryable(query).ToListAsync();

            if(specifications.IsNullOrEmpty())
            {
                return null;
            }

            var fundingStreams =  await _uow.GenericRepository<EntityModel.FundingStream>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            var fundingPeriods =  await _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingPeriodId).Contains(_.FundingPeriodId)).ToListAsync();

            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(_ => !_.IsDeleted && _.IsLatest && specifications.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions
                .Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            return BuildSpecificationModel(fundingStreams, fundingPeriods, specifications, specificationVersions, definitionSpecificationRelationships, variationPointers);
        }

        public bool IsConnectedToSQL()
        {
            return true;
        }

        public async Task<IEnumerable<SpecificationProfileVariationPointerModel>> GetProfileVariationPointers(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            return (from spec in _uow.context.Set<EntityModel.Specification>()
                    join fs in _uow.context.Set<EntityModel.FundingStream>() on spec.FundingStreamId equals fs.FundingStreamId
                    join sv in _uow.context.Set<EntityModel.SpecificationVersion>() on spec.SpecificationId equals sv.SpecificationId
                    join vp in _uow.context.Set<EntityModel.VariationPointer>() on sv.SpecificationVersionId equals vp.SpecificationVersionId
                    where spec.SpecificationId == specificationId && !vp.IsDeleted && sv.IsLatest
                    select new SpecificationProfileVariationPointerModel
                    {
                        FundingStreamId = fs.FundingStreamCode,
                        FundingLineId = vp.FundingLineId,
                        PeriodType = vp.PeriodType,
                        TypeValue = vp.PeriodValue,
                        Year = vp.Year,
                        Occurrence = vp.Occurrence
                    });
        }

        private static IEnumerable<Specification> BuildSpecificationModel(List<EntityModel.FundingStream> fundingStreams, List<EntityModel.FundingPeriod> fundingPeriods, List<EntityModel.Specification> specifications, List<EntityModel.SpecificationVersion> specificationVersions, List<EntityModel.DefinitionSpecificationRelationship> definitionSpecificationRelationships, List<EntityModel.VariationPointer> variationPointers)
        {
            return ((from specVersion in specificationVersions
                     join spec in specifications
                     on specVersion.SpecificationId equals spec.SpecificationId
                     join fundingPeriod in fundingPeriods
                     on spec.FundingPeriodId equals fundingPeriod.FundingPeriodId
                     join fundingStream in fundingStreams
                     on spec.FundingStreamId equals fundingStream.FundingStreamId
                     select new Specification()
                     {
                         Id = spec.SpecificationId,
                         ForceUpdateOnNextRefresh = spec.ForceUpdateOnNextRefresh,
                         Name = spec.SpecificationName,
                         IsSelectedForFunding = spec.IsSelectedForFunding,
                         Current = new Models.Specs.SpecificationVersion()
                         {
                             Name = specVersion.SpecificationName,
                             SpecificationId = spec.SpecificationId,
                             Author = new Reference
                             {
                                 Id = specVersion.AuthorId,
                                 Name = specVersion.AuthorName,
                             },
                             ExternalPublicationDate = specVersion.ExternalPublicationDate,
                             Description = specVersion.Description,
                             Comment = specVersion.Comment,
                             EarliestPaymentAvailableDate = specVersion.EarliestPaymentAvailableDate,
                             CoreProviderVersionUpdates = (CoreProviderVersionUpdates)Enum.Parse(typeof(CoreProviderVersionUpdates), specVersion.CoreProviderVersionUpdates),
                             Date = specVersion.Date,
                             ProviderSnapshotId = specVersion.ProviderSnapshotId,
                             ProviderSource = (ProviderSource)Enum.Parse(typeof(ProviderSource), specVersion.ProviderSource),
                             ProviderVersionId = specVersion.ProviderVersionId,
                             PublishStatus = (Models.Versioning.PublishStatus)Enum.Parse(typeof(PublishStatus), specVersion.PublishStatus),
                             Version = specVersion.Version,
                             UpdatedAt = specVersion.UpdatedAt,
                             TemplateIds = new Dictionary<string, string>() { { fundingStream.FundingStreamCode.ToString(), specVersion.TemplateVersion.ToString() } },
                             DataDefinitionRelationshipIds = definitionSpecificationRelationships.Where(_ => _.SpecificationVersionId == specVersion.SpecificationVersionId && !_.IsDeleted).Select(_ => _.DataDefinitionRelationshipId).ToList(),
                             FundingPeriod = new Reference()
                             {
                                 Id = fundingPeriod.FundingPeriodCode,
                                 Name = fundingPeriod.FundingPeriodName
                             },
                             FundingStreams = new List<Reference>()
                             {
                                 new Reference()
                                 {
                                     Id = fundingStream.FundingStreamCode,
                                     Name = fundingStream.FundingStreamName
                                 }
                             },
                             ProfileVariationPointers = variationPointers.Where(v => v.SpecificationVersionId == specVersion.SpecificationVersionId)
                             .Select(x =>
                                 new ProfileVariationPointer()
                                 {
                                     FundingStreamId = fundingStream.FundingStreamCode,
                                     FundingLineId = x.FundingLineId,
                                     PeriodType = x.PeriodType,
                                     TypeValue = x.PeriodValue,
                                     Year = x.Year,
                                     Occurrence = x.Occurrence
                                 }
                             ).ToList(),
                         }
                     }));
        }

        public async Task<IEnumerable<Specification>> GetSpecificationsWithProviderVersionUpdatesAsUseLatest(Expression<Func<EntityModel.SpecificationVersion, bool>> query)
        {
            var specificationVersions = await _uow.GenericRepository<EntityModel.SpecificationVersion>()
                .GetManyAsQueryable(query).ToListAsync();
            
            var specifications = await _uow.GenericRepository<EntityModel.Specification>()
                 .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationId).Contains(_.SpecificationId)).ToListAsync();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingStreamId).Contains(_.FundingStreamId)).ToListAsync();

            var fundingPeriods = await _uow.GenericRepository<EntityModel.FundingPeriod>()
                .GetManyAsQueryable(_ => specifications.Select(s => s.FundingPeriodId).Contains(_.FundingPeriodId)).ToListAsync();

            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions
                .Select(v => v.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            var variationPointers = await _uow.GenericRepository<EntityModel.VariationPointer>()
                .GetManyAsQueryable(_ => !_.IsDeleted && specificationVersions.Select(s => s.SpecificationVersionId).Contains(_.SpecificationVersionId)).ToListAsync();

            return BuildSpecificationModel(fundingStreams, fundingPeriods, specifications, specificationVersions, definitionSpecificationRelationships, variationPointers);
        }
    }
}
