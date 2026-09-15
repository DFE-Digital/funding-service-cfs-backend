using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.ApiClient.Policies.Models.FundingConfig;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Storage;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Profiling.Tests.TestHelpers;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using CalculateFunding.Services.Publishing.Interfaces;
using CalculateFunding.Services.Publishing.Models;
using CalculateFunding.Services.Publishing.ReprofilingOnDemand;
using CalculateFunding.Services.Publishing.Specifications;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NSubstitute;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.UnitTests.ReprofilingOnDemand
{
    [TestClass]
    public class ReprofilingStatusServiceTests
    {
        private const string BlobContainerName = "publishingconfirmation";

        private ReprofilingStatusService _service;
        private PublishedProviderIdsRequest _publishedProviderids;
        private ISpecificationIdServiceRequestValidator _validator;
        private ISpecificationService _specificationService;
        private IPublishedFundingRepository _publishedFundingRepository;
        private IPublishedProviderFundingCsvDataProcessor _fundingCsvDataProcessor;
        private IPublishedProviderReprofilingSummaryProcessor _publishedProviderReprofilingSummaryProcessor;
        private ICsvUtils _csvUtils;
        private IBlobClient _blobClient;
        private Mock<IPoliciesService> _policiesService;
        private IReleaseManagementRepository _releaseManagementRepository;
        private ILogger _logger;
        private string _specificationId;
        private ICreateJobsForReprofilingOnDemand _reprofilingOnDemandJobCreationService;

        private ValidationResult _validationResult;
        private IActionResult _actionResult;

        private SpecificationSummary _specificationSummary;
        private string _templateVersion;


        [TestInitialize]
        public void SetUp()
        {
            _specificationId = NewRandomString();
            _templateVersion = NewRandomString();
            _validationResult = new ValidationResult();
            _validator = Substitute.For<ISpecificationIdServiceRequestValidator>();
            _validator.Validate(_specificationId)
                .Returns(_validationResult);
            _policiesService = new Mock<IPoliciesService>();
            _publishedProviderids = new PublishedProviderIdsRequest()
            {
                PublishedProviderIds = new List<string>()
                {
                    "GAG-AC-2223-10084305",
                    "GAG-AC-2223-10084306"
                }
            };
            _specificationService = Substitute.For<ISpecificationService>();
            _publishedFundingRepository = Substitute.For<IPublishedFundingRepository>();
            _fundingCsvDataProcessor = Substitute.For<IPublishedProviderFundingCsvDataProcessor>();
            _publishedProviderReprofilingSummaryProcessor = Substitute.For<IPublishedProviderReprofilingSummaryProcessor>();
            _releaseManagementRepository = Substitute.For<IReleaseManagementRepository>();
            _csvUtils = Substitute.For<ICsvUtils>();
            _blobClient = Substitute.For<IBlobClient>();
            _logger = Substitute.For<ILogger>();
            _reprofilingOnDemandJobCreationService = Substitute.For<ICreateJobsForReprofilingOnDemand>();


            _service = new ReprofilingStatusService(_specificationService, _reprofilingOnDemandJobCreationService, _publishedFundingRepository, new ResiliencePolicies
            {
                PublishedFundingRepository = Polly.Policy.NoOpAsync(),
                SpecificationsRepositoryPolicy = Polly.Policy.NoOpAsync(),
                BlobClient = Polly.Policy.NoOpAsync()
            },
                _csvUtils,
                _blobClient,
                _publishedProviderReprofilingSummaryProcessor,
                _policiesService.Object,
                _logger);
        }


        private void GivenGetDistinctTemplateMetadataFundingLinesContents(
            string fundingStreamId,
            string fundingPeriodId,
            string templateVersion,
            TemplateMetadataDistinctFundingLinesContents templateMetadataDistinctFundingLinesContents)
        {
            _policiesService
                .Setup(_ => _.GetDistinctTemplateMetadataFundingLinesContents(
                    fundingStreamId,
                    fundingPeriodId,
                    templateVersion
                    ))
                .ReturnsAsync(templateMetadataDistinctFundingLinesContents)
                .Verifiable();
        }
        [TestMethod]
        public async Task ReturnsBadRequestWhenSuppliedSpecificationIdFailsValidation()
        {
            string[] expectedErrors = { NewRandomString(), NewRandomString() };
            string fundingStreamId1 = NewRandomString();
            string fundingStreamId2 = NewRandomString();
            string fundingLineCode = NewRandomString();
            string fundingPeriodId = NewRandomString();
            string fundingLineName = NewRandomString();
            string customProfileFundingLineCode = NewRandomString();
            string customProfileFundingLineName = NewRandomString();

            GivenGetDistinctTemplateMetadataFundingLinesContents(
               fundingStreamId1,
               fundingPeriodId,
               _templateVersion,
               new TemplateMetadataDistinctFundingLinesContents
               {
                   FundingLines = new List<TemplateMetadataFundingLine>
                   {
                        new TemplateMetadataFundingLine
                        {
                            FundingLineCode = fundingLineCode,
                            Name = fundingLineName
                        },
                         new TemplateMetadataFundingLine
                        {
                            FundingLineCode = customProfileFundingLineCode,
                            Name = customProfileFundingLineName
                        }
                   }
               });

            GivenTheValidationErrors(expectedErrors);

            AndTheSpecificationSummaryIsRetrieved(NewSpecificationSummary(s =>
            {
                s.WithId(_specificationId);
                s.WithFundingStreamIds(fundingStreamId1, fundingStreamId2);
                s.WithTemplateIds((fundingStreamId1, _templateVersion));
            }));


            await WhenThePublishedProvidersStatusAreQueried();

            ThenTheResponseShouldBe<OkObjectResult>();
        }

        private async Task WhenThePublishedProvidersStatusAreQueried()
        {
            _actionResult = await _service.GetProviderBatchResultForReprofiling(_publishedProviderids,_specificationId);
        }

        private void GivenTheValidationErrors(params string[] errors)
        {
            foreach (var error in errors) _validationResult.Errors.Add(new ValidationFailure(error, error));
        }

        private void ThenTheResponseShouldBe<TActionResult>(Expression<Func<TActionResult, bool>> matcher = null)
                where TActionResult : IActionResult
        {
            _actionResult
                .Should()
                .BeOfType<TActionResult>();

            if (matcher == null) return;

            ((TActionResult)_actionResult)
                .Should()
                .Match(matcher);
        }

        private string NewRandomString()
        {
            return new RandomString();
        }

        private int NewRandomNumber()
        {
            return new RandomNumberBetween(1, 10000);
        }
        private void AndTheSpecificationSummaryIsRetrieved(SpecificationSummary specificationSummary)
        {
            _specificationSummary = specificationSummary;
            _specificationService
                .GetSpecificationSummaryById(Arg.Is(_specificationId))
                .Returns(_specificationSummary);
        }


        private SpecificationSummary NewSpecificationSummary(Action<SpecificationSummaryBuilder> setUp = null)
        {
            SpecificationSummaryBuilder builder = new SpecificationSummaryBuilder();

            setUp?.Invoke(builder);

            return builder.Build();
        }

        private FundingConfiguration NewFundingConfiguration(Action<FundingConfigurationBuilder> setUp = null)
        {
            FundingConfigurationBuilder builder = new FundingConfigurationBuilder();

            setUp?.Invoke(builder);

            return builder.Build();
        }

    }
}
