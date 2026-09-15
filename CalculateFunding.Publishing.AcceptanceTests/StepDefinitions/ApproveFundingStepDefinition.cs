using Azure.Messaging.ServiceBus;
using CalculateFunding.Publishing.AcceptanceTests.Contexts;
using CalculateFunding.Publishing.AcceptanceTests.Extensions;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechTalk.SpecFlow;

namespace CalculateFunding.Publishing.AcceptanceTests.StepDefinitions
{
    [Binding]
    public class ApproveFundingStepDefinition
    {
        private readonly IPublishFundingStepContext _publishFundingStepContext;
        private readonly CurrentSpecificationStepContext _currentSpecificationStepContext;
        private readonly CurrentJobStepContext _currentJobStepContext;
        private readonly CurrentUserStepContext _currentUserStepContext;
        private readonly ICurrentCorrelationStepContext _currentCorrelationStepContext;
        private readonly IApproveService _approveService;

        public ApproveFundingStepDefinition(IPublishFundingStepContext publishFundingStepContext,
            CurrentSpecificationStepContext currentSpecificationStepContext,
            CurrentJobStepContext currentJobStepContext,
            CurrentUserStepContext currentUserStepContext, 
            IApproveService approveService,
            ICurrentCorrelationStepContext currentCorrelationStepContext)
        {
            _publishFundingStepContext = publishFundingStepContext;
            _currentSpecificationStepContext = currentSpecificationStepContext;
            _currentJobStepContext = currentJobStepContext;
            _currentUserStepContext = currentUserStepContext;
            _approveService = approveService;
            _currentCorrelationStepContext = currentCorrelationStepContext;
        }

        [When(@"funding is approved")]
        public async Task WhenFundingIsApproved()
        {
            _currentCorrelationStepContext.CorrelationId = Guid.NewGuid().ToString();
            var messageProperties = new Dictionary<string, object>
            {
                ["specification-id"] = _currentSpecificationStepContext.SpecificationId,
                ["jobId"] = _currentJobStepContext.JobId,
                ["user-id"] = _currentUserStepContext.UserId,
                ["user-id"] = _currentUserStepContext.UserId,
                ["user-name"] = _currentUserStepContext.UserName,
                ["sfa-correlationId"] = _currentCorrelationStepContext.CorrelationId,

            };
            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: new BinaryData(string.Empty),
                properties: messageProperties,
                messageId: Guid.NewGuid().ToString());
            await _approveService.Run(message);
        }

        [When(@"partial funding is approved")]
        public async Task WhenPartialFundingIsApproved(Table table)
        {
            string[] providerIds = table.AsStrings();
            PublishedProviderIdsRequest approveProvidersRequest = new PublishedProviderIdsRequest { PublishedProviderIds = providerIds };
            string approveProvidersRequestJson = JsonExtensions.AsJson(approveProvidersRequest);

            var messageProperties = new Dictionary<string, object>
            {
                ["specification-id"] = _currentSpecificationStepContext.SpecificationId,
                ["jobId"] = _currentJobStepContext.JobId,
                ["user-id"] = _currentUserStepContext.UserId,
                ["user-id"] = _currentUserStepContext.UserId,
                ["user-name"] = _currentUserStepContext.UserName,
                ["sfa-correlationId"] = _currentCorrelationStepContext.CorrelationId,

            };
            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: new BinaryData(approveProvidersRequestJson),
                properties: messageProperties,
                messageId: Guid.NewGuid().ToString());

            await _approveService.Run(message, async () =>
            {
                await _approveService.ApproveResults(message, batched: true);
            });

        }
    }
}
