using CalculateFunding.Common.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CalculateFunding.Models.Calcs
{
    public class AffectedCalculationResponseModel : Reference
    {
        [JsonProperty("calculationType")]
        public CalculationType CalculationType { get; set; }

        [JsonProperty("valueType")]
        public CalculationValueType ValueType { get; set; }

        [JsonProperty("removedDataFields")]
        public List<string> RemovedDataFields { get; set; }
    }
}
