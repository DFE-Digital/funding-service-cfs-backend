using System;
using System.Text.RegularExpressions;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Versioning;
using Newtonsoft.Json;

namespace CalculateFunding.Models.Calcs
{
    public class CalculationResponseModel : Reference
    {
        [JsonProperty("specificationId")]
        public string SpecificationId { get; set; }

        [JsonProperty("fundingStreamId")]
        public string FundingStreamId { get; set; }

        [JsonProperty("sourceCode")]
        public string SourceCode { get; set; }

        [JsonProperty("calculationType")]
        public CalculationType CalculationType { get; set; }

        [JsonProperty("sourceCodeName")]
        public string SourceCodeName { get; set; }

        [JsonProperty("namespace")]
        public CalculationNamespace Namespace { get; set; }

        [JsonProperty("wasTemplateCalculation")]
        public bool WasTemplateCalculation { get; set; }

        [JsonProperty("valueType")]
        public CalculationValueType ValueType { get; set; }

        [JsonProperty("dataType")]
        public CalculationDataType DataType { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTimeOffset? LastUpdated { get; set; }

        [JsonProperty("author")]
        public Reference Author { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("publishStatus")]
        public PublishStatus PublishStatus { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        public bool SearchPropertyInSourceCode(string propertyToSearch)
        {
            Regex commentRegex = new Regex(@"^\s*'|\s*`.*$", RegexOptions.Multiline);

            // Regular expression to match the property
            string propertyPattern = $@"(?<!\w){Regex.Escape(propertyToSearch)}(?!\w)";

            // Split the VBScript code into lines
            string[] lines = SourceCode.Split(new string[] { "\r\n" }, StringSplitOptions.None);

            // Initialize a flag to check if all lines are commented out
            bool allLinesCommented = true;

            foreach (string line in lines)
            {
                // Check if the line contains the property and is not entirely commented out
                if (Regex.IsMatch(line, propertyPattern) && !commentRegex.IsMatch(line))
                {
                    return true;
                }

                // Check if the line is entirely commented out
                if (!commentRegex.IsMatch(line))
                {
                    allLinesCommented = false;
                }
            }

            // If all lines are commented out, return false
            if (allLinesCommented)
            {
                return false;
            }

            return false;
        }
    }
}
