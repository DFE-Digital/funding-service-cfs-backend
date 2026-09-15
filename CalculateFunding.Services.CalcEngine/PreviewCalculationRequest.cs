using CalculateFunding.Models.Calcs;
using System.Collections.Generic;

namespace CalculateFunding.Services.CalcEngine
{
    public class PreviewCalculationRequest
    {
        public byte[] AssemblyContent { get; set; }
        public CalculationSummaryModel PreviewCalculationSummaryModel { get; set; }
        public IEnumerable<CalculationAggregationData> CalculationAggregationData { get; set; }
    }
}
