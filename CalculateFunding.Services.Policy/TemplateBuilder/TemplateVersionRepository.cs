using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Policy.TemplateBuilder;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Core.Services;
using System.Net;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Versioning;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace CalculateFunding.Services.Policy.TemplateBuilder
{
    public class TemplateVersionRepository : VersionRepository<TemplateVersion>, ITemplateVersionRepository
    {
        private readonly IUnitOfWork _uow;
        private readonly IConfiguration _configuration;
        private static ILogger _logger;
        public TemplateVersionRepository(ICosmosRepository cosmosRepository,
            INewVersionBuilderFactory<TemplateVersion> newVersionBuilderFactory, [Optional] IConfiguration configuration, [Optional] IUnitOfWork unitOfWork, [Optional] ILogger logger)
            : base(cosmosRepository, newVersionBuilderFactory)
        {
            _configuration = configuration;
            _uow = unitOfWork;
            _logger = logger;
        }

        public async Task<TemplateVersion> GetTemplateVersion(string templateId, int versionNumber)
        {
            Guard.IsNullOrWhiteSpace(templateId, nameof(templateId));

            var enableEfCoreSqlFlag = GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                var templateVersion = _uow.GenericRepository<EntityModel.TemplateVersion>()
                                                        .GetManyAsQueryable(x => x.TemplateId == new Guid(templateId) && x.IsDeleted == false);

                var templateVersionData = await GetTemplateVersionData(templateVersion, templateId);

                var templateVersionMatches = templateVersionData.Where(x => x.Version == versionNumber);

                if (templateVersionMatches == null || !templateVersionMatches.Any())
                {
                    return null;
                }

                if (templateVersionMatches.Count() == 1)
                {
                    return templateVersionMatches.First();
                }
                throw new ApplicationException($"Duplicate templates with TemplateId={templateId}");

            }

            // cosmos code
            IEnumerable<TemplateVersion> templateMatches = await CosmosRepository.Query<TemplateVersion>(x =>
                x.Content.TemplateId == templateId && x.Content.Version == versionNumber);

            if (templateMatches == null || !templateMatches.Any())
            {
                return null;
            }

            if (templateMatches.Count() == 1)
            {
                return templateMatches.First();
            }
            throw new ApplicationException($"Duplicate templates with TemplateId={templateId}");
        }

        public async Task<IEnumerable<TemplateVersion>> GetSummaryVersionsByTemplate(string templateId,
            IEnumerable<TemplateStatus> statuses)
        {
            var enableEfCoreSqlFlag = GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                Guard.IsNullOrWhiteSpace(templateId, nameof(templateId));
                List<TemplateStatus> templateStatus = statuses.ToList();
                IEnumerable<EntityModel.TemplateVersion> templateVersion = _uow.GenericRepository<EntityModel.TemplateVersion>().GetManyAsQueryable(x => x.TemplateId == new Guid(templateId) && x.IsDeleted == false);

                IEnumerable<TemplateVersion> getTemplateVersions = await GetTemplateVersionData(templateVersion, templateId);

                if (templateStatus.Any())
                {
                    var templateVersionByStatuses = getTemplateVersions.Where(x => templateStatus.Contains(x.Status));
                    return templateVersionByStatuses;
                }
                return getTemplateVersions;
            }


            //Cosmos Code
            Guard.IsNullOrWhiteSpace(templateId, nameof(templateId));

            List<TemplateStatus> templateStatuses = statuses.ToList();
            if (templateStatuses.Any())
            {
                return await CosmosRepository.Query<TemplateVersion>(x =>
                    x.Content.TemplateId == templateId && templateStatuses.Contains(x.Content.Status));
            }

            IEnumerable<TemplateVersion> versions = await CosmosRepository.Query<TemplateVersion>(x =>
                x.Content.TemplateId == templateId);

            return versions;

        }

        private async Task<IEnumerable<TemplateVersion>> GetTemplateVersionData(IEnumerable<EntityModel.TemplateVersion> templateVersion, string templateId)
        {
            //we use the firstordefault for templateVersion becaues Filter FundingStreamId FundingPeriod never chnanges in all version
            var templateVersionData = templateVersion.FirstOrDefault();
            var fundingStreamName = GetFundingStreamName(templateVersionData.FundingStreamId);
            var fundingPeriodName = GetFundingPeriodName(templateVersionData.FundingPeriodId);

            return await GetTemplatesVersions(templateVersion, fundingStreamName, fundingPeriodName);

        }

        private async Task<IEnumerable<TemplateVersion>> GetTemplatesVersions(IEnumerable<EntityModel.TemplateVersion> templateVersion, string fundingStreamName, string fundingPeriodName)
        {
            var templatePredecessors = await GetAllTemplatePredecessors(templateVersion.Select(_ => _.TemplateId));
            return templateVersion.Select(x => new TemplateVersion
            {
                TemplateId = x.TemplateId.ToString(),
                TemplateJson = x.TemplateJson,
                FundingStreamId = fundingStreamName,
                FundingPeriodId = fundingPeriodName,
                MajorVersion = x.MajorVersion,
                MinorVersion = x.MinorVersion,
                SchemaVersion = x.SchemaVersion,
                Name = x.Name,
                Status = (TemplateStatus)Enum.Parse(typeof(TemplateStatus), x.Status),
                Predecessors = templatePredecessors.TryGetValue(x.TemplateId, out List<string> value) ? value : null,
                Version = x.Version,
                Date = x.Date,
                Author = new Reference
                {
                    Id = x.AuthorId,
                    Name = x.AuthorName,
                },
                Comment = x.Comment,
                PublishStatus = (PublishStatus)Enum.Parse(typeof(PublishStatus), x.PublishStatus),

            });
        }

        public async Task<IEnumerable<TemplateVersion>> FindByFundingStreamAndPeriod(FindTemplateVersionQuery query)
        {
            var enableEfCoreSqlFlag = GetEnableEfCoreSqlFlag();

            if (enableEfCoreSqlFlag)
            {
                Guard.IsNullOrWhiteSpace(query.FundingStreamId, nameof(query.FundingStreamId));
                Guard.IsNullOrWhiteSpace(query.FundingPeriodId, nameof(query.FundingPeriodId));

                var fundingStreamId = GetFundingStreamId(query.FundingStreamId);
                var fundingPeriodId = GetFundingPeriodId(query.FundingPeriodId);

                var allTemplateVersion = _uow.GenericRepository<EntityModel.TemplateVersion>().GetManyAsQueryable(x => x.FundingStreamId == fundingStreamId
                                                                && x.FundingPeriodId == fundingPeriodId && x.IsDeleted == false);
                IEnumerable<TemplateVersion> getTemplateVersionsByFundingStreamAndPeriod = await GetTemplatesVersions(allTemplateVersion, query.FundingStreamId, query.FundingPeriodId);
                query.Statuses ??= new List<TemplateStatus>();
                if (query.Statuses.Any())
                {
                    return getTemplateVersionsByFundingStreamAndPeriod.Where(x => query.Statuses.Contains(x.Status));
                }
                return getTemplateVersionsByFundingStreamAndPeriod;
            }



            //cosmos code
            Guard.IsNullOrWhiteSpace(query.FundingStreamId, nameof(query.FundingStreamId));
            Guard.IsNullOrWhiteSpace(query.FundingPeriodId, nameof(query.FundingPeriodId));

            query.Statuses ??= new List<TemplateStatus>();
            if (query.Statuses.Any())
            {
                return await CosmosRepository.Query<TemplateVersion>(x =>
                    x.Content.FundingStreamId == query.FundingStreamId
                    && x.Content.FundingPeriodId == query.FundingPeriodId
                    && query.Statuses.Contains(x.Content.Status));
            }

            return await CosmosRepository.Query<TemplateVersion>(x =>
                x.Content.FundingStreamId == query.FundingStreamId
                && x.Content.FundingPeriodId == query.FundingPeriodId);
        }

        public async Task<HttpStatusCode> SaveTemplateVersion(TemplateVersion templateVersion)
        {
            var templateVersionRepo = _uow.GenericRepository<EntityModel.TemplateVersion>();
            EntityModel.TemplateVersion templateModel = new EntityModel.TemplateVersion
            {
                TemplateVersionId = templateVersion.Id,
                TemplateId = new Guid(templateVersion.TemplateId),
                //Description = templateVersion.Description, //Not required we stored in Template table and will get from same table 
                TemplateJson = templateVersion.TemplateJson,
                MinorVersion = templateVersion.MinorVersion,
                MajorVersion = templateVersion.MajorVersion,
                Name = templateVersion.Name,
                EntityId = templateVersion.EntityId,
                SchemaVersion = templateVersion.SchemaVersion,
                Status = templateVersion.Status.ToString(),
                Version = templateVersion.Version,
                Date = DateTime.Now,
                Comment = templateVersion.Comment,
                PublishStatus = templateVersion.PublishStatus.ToString(),
                UpdatedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
                FundingStreamId = GetFundingStreamId(templateVersion.FundingStreamId),
                FundingPeriodId = GetFundingPeriodId(templateVersion.FundingPeriodId),
                AuthorId = templateVersion.Author.Id,
                AuthorName = templateVersion.Author.Name,
            };

            templateVersionRepo.Insert(templateModel);

            _uow.Commit();
            return HttpStatusCode.OK;

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

        private string GetFundingPeriodName(int fundingPeriodId)
        {
            var fundingPeriodCode = _uow.GenericRepository<EntityModel.FundingPeriod>()
                                .GetSingleAsQueryable(x => x.FundingPeriodId == fundingPeriodId).FundingPeriodCode;
            return fundingPeriodCode;
        }

        private string GetFundingStreamName(int fundingStreamId)
        {
            var fundingStreamName = _uow.GenericRepository<EntityModel.FundingStream>().GetSingleAsQueryable(x => x.FundingStreamId == fundingStreamId).FundingStreamCode;
            return fundingStreamName;
        }

        private async Task<Dictionary<Guid, List<string>>> GetAllTemplatePredecessors(IEnumerable<Guid> templateIds)
        {
            return _uow.GenericRepository<EntityModel.TemplatePredecessor>()
                   .GetManyAsQueryable(x => templateIds.Contains(x.TemplateId)).ToList().GroupBy(obj => obj.TemplateId)
                      .ToDictionary(group => group.Key, group => group.Select(_ => _.TemplateVersionId).ToList());

        }

        private bool GetEnableEfCoreSqlFlag()
        {
            bool efCoreSqlFlag = _configuration.GetValue<bool>("UseSQLDB");
            _logger.Information($"Get EnableEfCoreSqlFlag From TemplateRepository :{efCoreSqlFlag}");
            return efCoreSqlFlag;
        }
    }
}