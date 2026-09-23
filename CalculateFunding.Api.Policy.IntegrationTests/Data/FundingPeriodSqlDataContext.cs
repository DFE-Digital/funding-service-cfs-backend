using CalculateFunding.IntegrationTests.Common.Data;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using CalculateFunding.Models.Policy;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using FundingPeriod = CalculateFunding.Models.Policy.FundingPeriod;
using Microsoft.Identity.Client;

namespace CalculateFunding.Api.Policy.IntegrationTests.Data
{
    public class FundingPeriodSqlDataContext : SqlDataContext
    {
        public FundingPeriodSqlDataContext(IConfiguration configuration) : base(configuration)
        {
        }

        public async Task CreateContextData(FundingPeriodParameters parameters)
        {
            FundingPeriod fundingPeriod = GetFundingPeriod(parameters);

            var entity = new EntityModel.FundingPeriod
            {
                FundingPeriodCode = fundingPeriod.Id,
                FundingPeriodName = fundingPeriod.Name,
                StartDate = fundingPeriod.StartDate.UtcDateTime,
                EndDate = fundingPeriod.EndDate.UtcDateTime,
                Type = fundingPeriod.Type.ToString(),
                StartYear = fundingPeriod.StartDate.Year,
                EndYear = fundingPeriod.EndDate.Year,
                Period = fundingPeriod.Period
            };

            await AddEntityAsync(entity);
        }

        public async Task RemoveContextData(FundingPeriodParameters parameters)
        {
            await RemoveEntitiesAsync<EntityModel.FundingPeriod>(_ => _.FundingPeriodCode == parameters.Id);
        }


        public FundingPeriod GetFundingPeriod(FundingPeriodParameters parameters)
        {
            return new FundingPeriod
            {
                Id = parameters.Id,
                Name = parameters.Name,
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddYears(1),
                Type = FundingPeriodType.FY,
                Period = DateTimeOffset.Now.Year.ToString().Substring(2,2) + DateTimeOffset.Now.AddYears(1).Year.ToString().Substring(2, 2),
            };
        }
    }
}
