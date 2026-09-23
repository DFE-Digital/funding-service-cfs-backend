using CalculateFunding.IntegrationTests.Common.Data;
using Microsoft.Extensions.Configuration;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using System.Threading.Tasks;

namespace CalculateFunding.Api.Policy.IntegrationTests.Data
{
    public class FundingStreamSqlDataContext : SqlDataContext
    {
        public FundingStreamSqlDataContext(IConfiguration configuration) : base(configuration)
        {
        }

        public async Task CreateContextData(FundingStreamParameters parameters)
        {
            var entity = new EntityModel.FundingStream
            {
                FundingStreamCode = parameters.Id,
                FundingStreamName = parameters.Name,
                ShortFundingStreamName = parameters.ShortName
            };

            await AddEntityAsync(entity);
        }
    }
}
