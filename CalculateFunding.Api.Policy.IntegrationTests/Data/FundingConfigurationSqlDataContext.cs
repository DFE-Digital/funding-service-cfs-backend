using CalculateFunding.IntegrationTests.Common.Data;
using CalculateFunding.Models.Policy.FundingPolicy;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Api.Policy.IntegrationTests.Data
{
    public class FundingConfigurationSqlDataContext : SqlDataContext
    {
        public FundingConfigurationSqlDataContext(IConfiguration configuration) : base(configuration)
        {
        }

        public async Task CreateContextData(FundingConfigurationParameters parameters)
        {

            var fundingStream = await GetFirstOrDefaultAsync<EntityModel.FundingStream>(_ => _.FundingStreamCode == parameters.FundingStreamId);

            var fundingPeriod = await GetFirstOrDefaultAsync<EntityModel.FundingPeriod>(_ => _.FundingPeriodCode == parameters.FundingPeriodId);

            var entity = new EntityModel.Policy
            {
                PolicyId = parameters.Id,
                DocumentType = nameof(FundingConfiguration),
                Content = JsonConvert.SerializeObject(GetFundingConfiguration(parameters)),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                FundingStreamId = fundingStream.FundingStreamId,
                FundingPeriodId = fundingPeriod.FundingPeriodId
            };

            await AddEntityAsync(entity);
        }

        private FundingConfiguration GetFundingConfiguration(FundingConfigurationParameters parameters)
        {
            return new FundingConfiguration
            {
                Id = parameters.Id,
                FundingPeriodId = parameters.FundingPeriodId,
                FundingStreamId = parameters.FundingStreamId,
                DefaultTemplateVersion = parameters.DefaultTemplateVersion,
                OrganisationGroupings = [],
                Variations = [],
                ErrorDetectors = [],
                ApprovalMode = ApprovalMode.All,
                ProviderSource = Models.Providers.ProviderSource.CFS,
                PaymentOrganisationSource = PaymentOrganisationSource.PaymentOrganisationAsProvider,
                UpdateCoreProviderVersion = UpdateCoreProviderVersion.Manual,
                EnableUserEditableCustomProfiles = false,
                EnableUserEditableRuleBasedProfiles = false,
                RunCalculationEngineAfterCoreProviderUpdate = false,
                EnableConverterDataMerge = false,
                IndicativeOpenerProviderStatus = [],
                AllowedPublishedFundingStreamsIdsToReference = parameters.AllowedPublishedFundingStreamsIdsToReference,
                ReleaseManagementVariations = parameters.ReleaseManagementVariations.ToList()
                .Select(_ => new VariationType { 
                    Name =  _.Name, 
                    Order = _.Order, 
                    FundingLineCodes = _.FundingLineCodes
                }),
                ReleaseChannels = parameters.ReleaseChannels.ToList()
                .Select(_ => new FundingConfigurationChannel
                {
                    OrganisationGroupings = _.OrganisationGroupings.ToList().Select(_ => new OrganisationGroupingConfiguration { 
                        GroupTypeIdentifier = (OrganisationGroupTypeIdentifier)_.GroupTypeIdentifier,
                        GroupingReason = (GroupingReason)_.GroupingReason,
                        GroupTypeClassification = (OrganisationGroupTypeClassification)_.GroupTypeClassification,
                        OrganisationGroupTypeCode = (OrganisationGroupTypeCode)_.OrganisationGroupTypeCode,
                        ProviderTypeMatch = _.ProviderTypeMatch.Select(_ => new ProviderTypeMatch { ProviderType = _.ProviderType, ProviderSubtype = _.ProviderSubtype}),
                        ProviderStatus  = _.ProviderStatus
                    }),
                    ChannelCode = _.ChannelCode,
                    ProviderTypeMatch = _.ProviderTypeMatch.Select(_ => new ProviderTypeMatch { ProviderType = _.ProviderType, ProviderSubtype = _.ProviderSubtype }),
                    ProviderStatus = _.ProviderStatus,
                    IsVisible = _.IsVisible
                }),
                ReleaseActionGroups = parameters.ReleaseActionGroups.ToList()
                .Select(_ => new ReleaseActionGroup
                {
                    SortOrder = _.SortOrder,
                    Description = _.Description,
                    ChannelCodes = _.ChannelCodes,
                })
            };

        }
    }
}
