using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Policy;
using CalculateFunding.Models.Policy.TemplateBuilder;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Policy.Interfaces;
using Microsoft.Extensions.Configuration;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Serilog;
using System.Collections.Concurrent;
using CalculateFunding.Common.Models.HealthCheck;

namespace CalculateFunding.Services.Policy.TemplateBuilder
{
    public class TemplateRepository : IHealthChecker, ITemplateRepository
    {
        private readonly IConfiguration _configuration;
        private static ILogger _logger;
        protected readonly ICosmosRepository _cosmosRepository;
        protected readonly IUnitOfWork _uow;


        public TemplateRepository(ICosmosRepository cosmosRepository, IConfiguration configuration, [Optional] IUnitOfWork uow, [Optional] ILogger logger)
        {
            Guard.ArgumentNotNull(cosmosRepository, nameof(cosmosRepository));
            Guard.ArgumentNotNull(uow, nameof(uow));
            _configuration = configuration;
            _logger = logger;
            _cosmosRepository = cosmosRepository;
            _uow = uow;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            var enableEfCoreSqlFlag = GetEnableEfCoreSqlFlag().Result;
            if (enableEfCoreSqlFlag)
            {
                bool canConnect = _uow.context.Database.CanConnect();
                ServiceHealth serviceHealth = new ServiceHealth()
                {
                    Name = nameof(TemplateRepository)
                };
                serviceHealth.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });
                return serviceHealth;
            }

            var health = new ServiceHealth
            {
                Name = GetType().Name
            };
            (bool Ok, string Message) = _cosmosRepository.IsHealthOk();
            health.Dependencies.Add(new DependencyHealth { HealthOk = Ok, DependencyName = GetType().Name, Message = Message });

            return health;
        }

        public async Task<ServiceHealth> GetHealthStatus()
        {
            return await IsHealthOk();
        }

        private async Task<bool> GetEnableEfCoreSqlFlag()
        {
            bool efCoreSqlFlag = _configuration.GetValue<bool>("UseSQLDB");
            _logger.Information($"Get EnableEfCoreSqlFlag From TemplateRepository :{efCoreSqlFlag}");
            return efCoreSqlFlag;
        }

        public async Task<Template> GetTemplate(string templateId)
        {
            Guard.IsNullOrWhiteSpace(templateId, nameof(templateId));

            var enableEfCoreSqlFlag = await GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                var gettemplates = _uow.GenericRepository<EntityModel.Template>().GetManyAsQueryable(x => x.TemplateId == new Guid(templateId) && x.IsDeleted == false);

                IEnumerable<Template> templates = await GetTemplateData(gettemplates);

                if (templates != null && templates.Any())
                {
                    if (templates.Count() == 1)
                        return templates.First();
                    throw new ApplicationException($"Duplicate templates with TemplateId={templateId}");
                }
                return null;

            }
            //Cosmos Code
            IEnumerable<Template> templateMatches = await _cosmosRepository
               .Query<Template>(x => x.Content.TemplateId == templateId);

            if (templateMatches != null && templateMatches.Any())
            {
                if (templateMatches.Count() == 1)
                    return templateMatches.First();

                throw new ApplicationException($"Duplicate templates with TemplateId={templateId}");
            }

            return null;

        }

        private async Task<IEnumerable<Template>> GetTemplateData(IEnumerable<EntityModel.Template> templateMatches)
        {
            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>().Get();
            var fundingPeriod = _uow.GenericRepository<EntityModel.FundingPeriod>().Get();

            var templatePredecessors = await GetAllTemplatePredecessors(templateMatches.Select(_ => _.TemplateId));

            return (from template in templateMatches
                    join fs in fundingStream
                    on template.FundingStreamId equals fs.FundingStreamId
                    join fp in fundingPeriod
                    on template.FundingPeriodId equals fp.FundingPeriodId
                    select new Template()
                    {

                        TemplateId = template.TemplateId.ToString(),
                        Description = template.Description,
                        Name = template.Name,
                        FundingStream = new FundingStream
                        {
                            ShortName = fs.ShortFundingStreamName,
                            Id = fs.FundingStreamCode,
                            Name = fs.ShortFundingStreamName,

                        },
                        FundingPeriod = new FundingPeriod
                        {
                            StartDate = new DateTimeOffset(fp.StartDate.Value),
                            EndDate = new DateTimeOffset(fp.EndDate.Value),
                            Period = fp.Period,
                            Type = (FundingPeriodType)Enum.Parse(typeof(FundingPeriodType), fp.Type),
                            Id = fp.FundingPeriodCode,
                            Name = fp.FundingPeriodName,
                        },
                        Current = new TemplateVersion
                        {
                            TemplateId = template.TemplateId.ToString(),
                            TemplateJson = template.TemplateJson,
                            FundingStreamId = fs.FundingStreamCode,
                            FundingPeriodId = fp.FundingPeriodCode,
                            MajorVersion = template.MajorVersion,
                            MinorVersion = template.MinorVersion,
                            SchemaVersion = template.SchemaVersion,
                            Name = template.Name,
                            Status = (TemplateStatus)Enum.Parse(typeof(TemplateStatus), template.Status),
                            Predecessors = templatePredecessors.TryGetValue(template.TemplateId, out List<string> value) ? value : null,
                            Version = template.Version,
                            Date = DateTime.Now,
                            Author = new Reference
                            {
                                Id = template.AuthorId,
                                Name = template.AuthorName,
                            },
                            Comment = template.Comment,
                            PublishStatus = (PublishStatus)Enum.Parse(typeof(PublishStatus), template.PublishStatus)
                        }

                    });
        }

        private async Task<Dictionary<Guid, List<string>>> GetAllTemplatePredecessors(IEnumerable<Guid> templateIds)
        {
            return _uow.GenericRepository<EntityModel.TemplatePredecessor>()
                   .GetManyAsQueryable(x => templateIds.Contains(x.TemplateId)).ToList().GroupBy(obj => obj.TemplateId)
                      .ToDictionary(group => group.Key, group => group.Select(_ => _.TemplateVersionId).ToList());

        }

        public async Task<HttpStatusCode> CreateDraft(Template template)
        {
            var enableEfCoreSqlFlag = await GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                var templateRepo = _uow.GenericRepository<EntityModel.Template>();
                EntityModel.Template templateModel = new EntityModel.Template
                {
                    TemplateId = new Guid(template.TemplateId),
                    Description = template.Description,
                    TemplateJson = template.Current.TemplateJson,
                    MinorVersion = template.Current.MinorVersion,
                    MajorVersion = template.Current.MajorVersion,
                    Name = template.Current.Name,
                    EntityId = template.Current.EntityId,
                    SchemaVersion = template.Current.SchemaVersion,
                    Status = template.Current.Status.ToString(),
                    Version = template.Current.Version,
                    Date = DateTime.Now,
                    Comment = template.Current.Comment,
                    PublishStatus = template.Current.PublishStatus.ToString(),
                    UpdatedAt = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    FundingStreamId = GetFundingStreamId(template.Current.FundingStreamId),
                    FundingPeriodId = GetFundingPeriodId(template.Current.FundingPeriodId),
                    AuthorId = template.Current.Author.Id,
                    AuthorName = template.Current.Author.Name,
                };

                templateRepo.Insert(templateModel);

                var templatepredecessorRepo = _uow.GenericRepository<EntityModel.TemplatePredecessor>();

                //1st time template its in draft so no TemplateJson is avaible and doesnot have TemplatePredecessor  /// how to manage the clone beuse in clone
                // temlatejson is not empty 
                if (templateModel.TemplateJson != null)
                {
                    EntityModel.TemplatePredecessor templatepredecessor = new EntityModel.TemplatePredecessor
                    {
                        TemplateId = new Guid(template.TemplateId),
                        TemplateVersionId = template.Current.Id
                    };
                    templatepredecessorRepo.Insert(templatepredecessor);
                }
                return HttpStatusCode.Created;
            }

            //cosmos code
            return await _cosmosRepository.CreateAsync(template);
        }

        private int GetFundingPeriodId(string fundingPeriodId)
        {
            var periodCode = _uow.GenericRepository<EntityModel.FundingPeriod>()
                                .GetSingleAsQueryable(x => x.FundingPeriodCode == fundingPeriodId).FundingPeriodId;
            return periodCode;
        }

        private int GetFundingStreamId(string fundingStreamId)
        {
            var StreamId = _uow.GenericRepository<EntityModel.FundingStream>()
                              .GetSingleAsQueryable(x => x.FundingStreamCode == fundingStreamId).FundingStreamId;
            return StreamId;
        }

        public async Task<HttpStatusCode> Update(Template template)
        {
            var enableEfCoreSqlFlag = await GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                Guard.ArgumentNotNull(template, nameof(template));
                Guard.ArgumentNotNull(template.Id, nameof(template.Id));
                var templateRepo = _uow.GenericRepository<EntityModel.Template>();
                var templateData = _uow.GenericRepository<EntityModel.Template>().GetSingleAsQueryable(x => x.TemplateId == new Guid(template.Current.TemplateId));

                templateData.Description = template.Description;
                templateData.TemplateJson = template.Current.TemplateJson;
                templateData.MinorVersion = template.Current.MinorVersion;
                templateData.MajorVersion = template.Current.MajorVersion;
                templateData.Name = template.Current.Name;
                templateData.EntityId = template.Current.EntityId;
                templateData.SchemaVersion = template.Current.SchemaVersion;
                templateData.Status = template.Current.Status.ToString();
                templateData.Version = template.Current.Version;
                templateData.Date = DateTime.Now;
                templateData.Comment = template.Current.Comment;
                templateData.PublishStatus = template.Current.PublishStatus.ToString();
                templateData.UpdatedAt = DateTime.Now;
                templateData.FundingStreamId = GetFundingStreamId(template.Current.FundingStreamId);
                templateData.FundingPeriodId = GetFundingPeriodId(template.Current.FundingPeriodId);
                templateData.AuthorId = template.Current.Author.Id;
                templateData.AuthorName = template.Current.Author.Name;

                templateRepo.Update(templateData);
                return HttpStatusCode.Accepted;
            }

            //cosmos code
            Guard.ArgumentNotNull(template, nameof(template));
            Guard.ArgumentNotNull(template.Id, nameof(template.Id));
            return await _cosmosRepository.UpdateAsync(template);
        }

        public async Task<bool> IsFundingStreamAndPeriodInUse(string fundingStreamId, string fundingPeriodId)
        {
            var enableEfCoreSqlFlag = await GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
                Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));

                var allTemplates = await GetAllTemplates();
                var existingTemplates = allTemplates.Where(x => x.Current != null &&
                                                              x.Current.FundingStreamId == fundingStreamId &&
                                                              x.Current.FundingPeriodId == fundingPeriodId);
                return existingTemplates.Any();
            }


            //cosmoscode
            Guard.IsNullOrWhiteSpace(fundingStreamId, nameof(fundingStreamId));
            Guard.IsNullOrWhiteSpace(fundingPeriodId, nameof(fundingPeriodId));

            IEnumerable<Template> existing = await _cosmosRepository.Query<Template>(x =>
                x.Content.Current != null &&
                x.Content.Current.FundingStreamId == fundingStreamId &&
                x.Content.Current.FundingPeriodId == fundingPeriodId);

            return existing.Any();
        }

        public async Task GetTemplatesForIndexing(Func<List<Template>, Task> persistIndexBatch, int batchSize)
        {

            //cosmos code
            CosmosDbQuery query = new CosmosDbQuery
            {
                QueryText = @"SELECT
                    c.content.templateId,
                    c.content.name,
                    { 
                       'name'         : c.content.name,
                       'templateId'   : c.content.current.templateId,
                       'date'         : c.content.current.date,
                       'majorVersion' : c.content.current.majorVersion,
                       'minorVersion' : c.content.current.minorVersion,
                       'status'       : c.content.current.status,
                       'version'      : c.content.current.version,
                       'author'       : {
                          'id'           : c.content.current.author.id,
                          'name'         : c.content.current.author.name
                        }
                    } AS Current,
                    { 
                       'majorVersion' : c.content.released.majorVersion,
                       'minorVersion' : c.content.released.minorVersion,
                       'status'       : c.content.released.status,
                       'version'      : c.content.released.version
                    } AS Released,
                    {
                       'id'           : c.content.fundingStream.id,
                       'name'         : c.content.fundingStream.name,
                       'shortName'    : c.content.fundingStream.shortName
                    } AS FundingStream,
                    {
                       'id'           : c.content.fundingPeriod.id,
                       'name'         : c.content.fundingPeriod.name,
                       'shortName'    : c.content.fundingPeriod.shortName
                    } AS FundingPeriod
            FROM     templateBuilder c
            WHERE    c.documentType = 'Template' 
            AND      c.deleted = false"
            };

            await _cosmosRepository.DocumentsBatchProcessingAsync(persistBatchToIndex: persistIndexBatch,
                cosmosDbQuery: query,
                itemsPerPage: batchSize);

        }

        public async Task<IEnumerable<Template>> GetAllTemplates([Optional] string fundingStreamCode)
        {
            var enableEfCoreSqlFlag = await GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                if (string.IsNullOrWhiteSpace(fundingStreamCode))
                {
                    var templateData = _uow.GenericRepository<EntityModel.Template>().GetManyAsQueryable(x => x.IsDeleted == false);
                    var getAllTemplateData = await GetTemplateData(templateData);
                    return getAllTemplateData;
                }

                var templateMatches = _uow.GenericRepository<EntityModel.Template>().GetManyAsQueryable(x => x.FundingStream.FundingStreamCode == fundingStreamCode && x.IsDeleted == false);
                var getAllTemplates = await GetTemplateData(templateMatches);
                return getAllTemplates;
            }

            //cosmos code
            IEnumerable<Template> templates = await _cosmosRepository.Query<Template>();

            return templates;

        }
        public async Task<HttpStatusCode> SaveTemplatePredecessor(string templateId, string templatePredecessorId)
        {
            var templatePredecessorRepo = _uow.GenericRepository<EntityModel.TemplatePredecessor>();
            var checkTemplatePredecessorId = templatePredecessorRepo.GetFirstorDefault(x =>x.TemplateVersionId == templatePredecessorId);
            if (checkTemplatePredecessorId == null)
            {
                EntityModel.TemplatePredecessor templatePredecessor = new EntityModel.TemplatePredecessor
                {
                    TemplateId = new Guid(templateId),
                    TemplateVersionId = templatePredecessorId
                };
                templatePredecessorRepo.Insert(templatePredecessor);
                return HttpStatusCode.Created;
            }
            return HttpStatusCode.OK;
        }

        public Task<IEnumerable<Template>> GetTemplatesFromSqlForIndexing()
        {
            return GetAllTemplates();
        }

        public async Task<HttpStatusCode> UpdateDescription(Template template)
        {
            var enableEfCoreSqlFlag = await GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                Guard.ArgumentNotNull(template, nameof(template));
                Guard.ArgumentNotNull(template.Id, nameof(template.Id));
                var templateRepo = _uow.GenericRepository<EntityModel.Template>();
                var templateData = _uow.GenericRepository<EntityModel.Template>().GetSingleAsQueryable(x => x.TemplateId == new Guid(template.Current.TemplateId));

                templateData.Description = template.Description;                            
                templateData.UpdatedAt = DateTime.Now;              
                templateData.AuthorId = template.Current.Author.Id;
                templateData.AuthorName = template.Current.Author.Name;

                templateRepo.Update(templateData);
                _uow.Commit();
                return HttpStatusCode.Accepted;
            }

            //cosmos code
            Guard.ArgumentNotNull(template, nameof(template));
            Guard.ArgumentNotNull(template.Id, nameof(template.Id));
            return await _cosmosRepository.UpdateAsync(template);
        }

    }
}