using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Datasets
{
    public class UpdateFDSDataSchemaResponseModel
    {
        [JsonProperty("datasetDefinitionId")]
        public string DatasetDefintionId { get; set; }

        [JsonProperty("current")]
        public DefinitionSpecificationRelationshipVersion Current { get; set; }

        [JsonProperty("affectedCalculations")]
        public IEnumerable<AffectedCalculationResponseModel> AffectedCalculations { get; set; }
    }
}
