using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Specs.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;

namespace CalculateFunding.Services.Specs.UnitTests.Services
{
    public partial class SpecificationsServiceTests
    {
        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenNoFundingPeriodId_ReturnsBadRequestObject()
        {
            //Arrange
            ILogger logger = CreateLogger();

            SpecificationsService service = CreateService(logs: logger);

            //Act
            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(null, null);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("Null or empty fundingPeriodId provided");

            logger
                .Received(1)
                .Error(Arg.Is("No funding period Id was provided to GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId"));
        }

        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenNoFundingStreamId_ReturnsBadRequestObject()
        {
            //Arrange
            ILogger logger = CreateLogger();

            SpecificationsService service = CreateService(logs: logger);

            //Act
            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(FundingPeriodId, null);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("Null or empty fundingStreamId provided");

            logger
                .Received(1)
                .Error(Arg.Is("No funding stream Id was provided to GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId"));
        }

        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenFundingStreamIdAndFundingPeriodId_ReturnsSpecificationSummary()
        {
            string fundingStreamId = NewRandomString();
            string fundingPeriodId = NewRandomString();
            string specificationId = NewRandomString();

            IMapper mapper = CreateImplementedMapper();
            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();

            IEnumerable<Specification> specifications = NewSpecifications(_ => _.WithId(specificationId));

            specificationsRepository
                .GetSpecificationsByFundingPeriodAndFundingStream(fundingPeriodId, fundingStreamId)
                .Returns(specifications);

            SpecificationsService service = CreateService(
                mapper: mapper,
                specificationsRepository: specificationsRepository);

            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(fundingPeriodId, fundingStreamId);

            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeOfType<List<SpecificationSummary>>()
                .And
                .NotBeNull();

            List<SpecificationSummary> objContent = (List<SpecificationSummary>)((OkObjectResult)result).Value;
            SpecificationSummary specificationSummary = objContent.FirstOrDefault();

            specificationSummary
                .Should()
                .NotBeNull();

            specificationSummary
                .Id
                .Should()
                .Be(specificationId);
        }
    }
}
