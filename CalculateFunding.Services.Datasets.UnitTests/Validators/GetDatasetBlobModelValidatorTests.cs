using CalculateFunding.Models.Datasets;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Datasets.Services;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using PoliciesApiModels = CalculateFunding.Common.ApiClient.Policies.Models;

namespace CalculateFunding.Services.Datasets.Validators
{
    [TestClass]
    public class GetDatasetBlobModelValidatorTests
    {
        const string FundingStreamId = "funding-stream-id";
        const string FundingStreamName = "funding-stream-name";

        [TestMethod]
        public void Validate_GivenMissingDatasetId_ReturnsFalse()
        {
            //Arrange
            GetDatasetBlobModel model = CreateModel();
            model.DatasetId = string.Empty;

            GetDatasetBlobModelValidator validator = CreateValidator();

            //Act
            ValidationResult result = validator.Validate(model);

            //Assert
            result
                .IsValid
                .Should()
                .BeFalse();

            result
                .Errors
                .Count
                .Should()
                .Be(1);
        }

        [TestMethod]
        public void Validate_GivenMissingFilename_ReturnsFalse()
        {
            //Arrange
            GetDatasetBlobModel model = CreateModel();
            model.Filename = string.Empty;

            GetDatasetBlobModelValidator validator = CreateValidator();

            //Act
            ValidationResult result = validator.Validate(model);

            //Assert
            result
                .IsValid
                .Should()
                .BeFalse();

            result
                .Errors
                .Count
                .Should()
                .Be(1);
        }

        [TestMethod]
        public void Validate_GivenMissingFundingStreamId_ReturnsFalse()
        {
            //Arrange
            GetDatasetBlobModel model = CreateModel();
            model.FundingStreamId = string.Empty;

            GetDatasetBlobModelValidator validator = CreateValidator();

            //Act
            ValidationResult result = validator.Validate(model);

            //Assert
            result
                .IsValid
                .Should()
                .BeFalse();

            result
                .Errors
                .Count
                .Should()
                .Be(1);
        }

        [TestMethod]
        public void Validate_GivenInvalidFundingStreamId_ValidIsFalse()
        {
            //Arrange
            GetDatasetBlobModel model = CreateModel();
            model.FundingStreamId = "test-invalid-funding-stream-id";

            GetDatasetBlobModelValidator validator = CreateValidator();

            //Act
            ValidationResult result = validator.Validate(model);

            //Assert
            result
                .IsValid
                .Should()
                .BeFalse();

            result
                .Errors
                .Count
                .Should()
                .Be(1);
        }

        [TestMethod]
        public void Validate_GivenVersionIsZero_ReturnsFalse()
        {
            //Arrange
            GetDatasetBlobModel model = CreateModel();
            model.Version = 0;

            GetDatasetBlobModelValidator validator = CreateValidator();

            //Act
            ValidationResult result = validator.Validate(model);

            //Assert
            result
                .IsValid
                .Should()
                .BeFalse();

            result
                .Errors
                .Count
                .Should()
                .Be(1);
        }

        [TestMethod]
        public void Validate_GivenValidModel_ReturnsTrue()
        {
            //Arrange
            GetDatasetBlobModel model = CreateModel();

            GetDatasetBlobModelValidator validator = CreateValidator();

            //Act
            ValidationResult result = validator.Validate(model);

            //Assert
            result
                .IsValid
                .Should()
                .BeTrue();
        }

        static GetDatasetBlobModel CreateModel()
        {
            return new GetDatasetBlobModel
            {
                DatasetId = "data-set-id",
                Filename = "any-file.csv",
                Version = 1,
                FundingStreamId = FundingStreamId
            };
        }

        static GetDatasetBlobModelValidator CreateValidator(
            IPolicyRepository policyRepository = null)
        {
            return new GetDatasetBlobModelValidator(
                policyRepository ?? CreatePolicyRepository());
        }

        static IPolicyRepository CreatePolicyRepository()
        {
            IPolicyRepository repository = Substitute.For<IPolicyRepository>();

            repository
                .GetFundingStreams()
                .Returns(NewFundingStreams());

            return repository;
        }

        protected static IEnumerable<PoliciesApiModels.FundingStream> NewFundingStreams() =>
            new List<PoliciesApiModels.FundingStream>
            {
                NewApiFundingStream(_ => _.WithId(FundingStreamId).WithName(FundingStreamName))
            };

        protected static PoliciesApiModels.FundingStream NewApiFundingStream(
            Action<PolicyFundingStreamBuilder> setUp = null)
        {
            PolicyFundingStreamBuilder fundingStreamBuilder = new PolicyFundingStreamBuilder();

            setUp?.Invoke(fundingStreamBuilder);

            return fundingStreamBuilder.Build();
        }
    }
}
