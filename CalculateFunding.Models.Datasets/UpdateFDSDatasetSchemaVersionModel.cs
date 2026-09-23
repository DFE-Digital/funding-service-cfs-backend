using System.Collections.Generic;

namespace CalculateFunding.Models.Datasets
{
    public class UpdateFDSDatasetSchemaVersionModel
    {
        public string RelationshipId { get; set; }

        public string DatasetDefintionId { get; set; }

        public List<DatasetComparisonField> RemovedFields { get; set; }
    }
}
