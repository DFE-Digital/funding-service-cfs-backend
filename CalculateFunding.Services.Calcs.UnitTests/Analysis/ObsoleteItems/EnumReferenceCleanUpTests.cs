using CalculateFunding.Models.Calcs.ObsoleteItems;
using CalculateFunding.Services.Calcs.Analysis.ObsoleteItems;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Polly;

namespace CalculateFunding.Services.Calcs.UnitTests.Analysis.ObsoleteItems
{
    [TestClass]
    public class EnumReferenceCleanUpTests : ObsoleteItemCleanUpTest
    {
        [TestInitialize]
        public void SetUp()
        {
            ObsoleteItemType = ObsoleteItemType.EnumValue;
            
            CleanUp = new EnumReferenceCleanUp(Calculations.Object,
                new ResiliencePolicies
                {
                    CalculationsRepository = Policy.NoOpAsync(),
                    CalculationsRepositoryNoOCCRetry = Policy.NoOpAsync()
                });
        }
    }
}