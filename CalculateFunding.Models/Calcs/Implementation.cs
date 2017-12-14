using System.Collections.Generic;
using CalculateFunding.Models.Specs;
using Newtonsoft.Json;

namespace CalculateFunding.Models.Calcs
{
    public class Implementation : Reference
    {
        [JsonProperty("specification")]
        public Reference Specification { get; set; }

        [JsonProperty("targetLanguage")]
        public TargetLanguage TargetLanguage { get; set; }

        [JsonProperty("calculations")]
        public List<CalculationImplementation> Calculations { get; set; }

        [JsonProperty("datasetDefinitions")]
        public List<DatasetDefinition> DatasetDefinitions { get; set; }
    }
}