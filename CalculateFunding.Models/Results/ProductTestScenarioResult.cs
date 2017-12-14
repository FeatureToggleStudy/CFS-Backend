﻿using System;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Specs;
using Newtonsoft.Json;

namespace CalculateFunding.Models.Results
{
    public class ProductTestScenarioResult
    {
        [JsonProperty("fundingPolicy")]
        public Reference FundingPolicy { get; set; }
        [JsonProperty("allocationLine")]
        public Reference AllocationLine { get; set; }
        [JsonProperty("productFolder")]
        public Reference ProductFolder { get; set; }
        [JsonProperty("product")]
        public CalculationImplementation Product { get; set; }
        [JsonProperty("scenarioName")]
        public Reference Scenario { get; set; }
        [JsonProperty("testResult")]
        public TestResult TestResult { get; set; }

        [JsonProperty("lastFailedDate")]
        public DateTime? LastFailedDate { get; set; }

        [JsonProperty("lastPassedDate")]
        public DateTime? LastPassedDate { get; set; }

        [JsonProperty("tags")]
        public string[] Tags { get; set; }
        [JsonProperty("stepsExecuted")]
        public int StepExected { get; set; }
        [JsonProperty("totalSteps")]
        public int TotalSteps { get; set; }
        [JsonProperty("datasetReferences")]
        public DatasetReference[] DatasetReferences { get; set; }
        [JsonProperty("productValue")]
        public decimal? ProductValue { get; set; }
    }
}