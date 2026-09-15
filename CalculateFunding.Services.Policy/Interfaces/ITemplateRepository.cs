using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Models.Policy.TemplateBuilder;

namespace CalculateFunding.Services.Policy.Interfaces
{
    public interface ITemplateRepository
    {
        Task<HttpStatusCode> CreateDraft(Template template);
        Task<Template> GetTemplate(string templateId);
        Task<HttpStatusCode> Update(Template template);
        Task<bool> IsFundingStreamAndPeriodInUse(string fundingStreamId, string fundingPeriodId);
        Task GetTemplatesForIndexing(Func<List<Template>, Task> persistIndexBatch, int batchSize);
        Task<IEnumerable<Template>> GetAllTemplates([Optional] string fundingStreamId);
        Task<HttpStatusCode> SaveTemplatePredecessor(string templateId, string templatePredecessorId);
        Task<IEnumerable<Template>> GetTemplatesFromSqlForIndexing();
        Task<HttpStatusCode> UpdateDescription(Template template);
        Task<ServiceHealth> GetHealthStatus();
    }
}