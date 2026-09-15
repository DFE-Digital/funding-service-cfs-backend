using CalculateFunding.Models.Calcs;
using CalculateFunding.Tests.Common.Helpers;

namespace CalculateFunding.Services.Datasets.Services
{
    public class CalculationResponseBuilder : TestEntityBuilder
    {
        private string _id;
        private string _name;
        private string _sourceCode;

        public CalculationResponseBuilder WithSourceCode(string sourceCode)
        {
            _sourceCode = sourceCode;

            return this;
        }

        public CalculationResponseBuilder WithId(string id)
        {
            _id = id;
            return this;
        }

        public CalculationResponseBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public CalculationResponseModel Build()
        {
            return new CalculationResponseModel
            {
                Id = _id,
                Name = _name,
                SourceCode = _sourceCode
            };
        }
    }
}