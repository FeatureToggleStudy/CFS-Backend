﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.Search;
using Newtonsoft.Json;

namespace CalculateFunding.Models.Calcs
{
    [SearchIndex(IndexerForType = typeof(Calculation),
        CollectionName = "results",
        DatabaseName = "allocations")]
    public class CalculationIndex
    {
        [Key]
        [IsSearchable]
        [JsonProperty("id")]
        public string Id { get; set; }

        [IsSearchable]
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("calculationSpecificationId")]
        public string CalculationSpecificationId { get; set; }

        [IsFilterable, IsSortable, IsSearchable]
        [JsonProperty("calculationSpecificationName")]
        public string CalculationSpecificationName { get; set; }

        [IsFilterable, IsSortable, IsSearchable]
        [JsonProperty("specificationName")]
        public string SpecificationName { get; set; }

        [JsonProperty("specificationId")]
        public string SpecificationId { get; set; }

        [IsFilterable, IsSortable, IsSearchable]
        [JsonProperty("periodName")]
        public string PeriodName { get; set; }

        [IsFilterable]
        [JsonProperty("periodId")]
        public string PeriodId { get; set; }

        [IsFilterable, IsSortable, IsFacetable, IsSearchable]
        [JsonProperty("allocationLineName")]
        public string AllocationLineName { get; set; }

        [JsonProperty("allocationLineId")]
        public string AllocationLineId{ get; set; }

        [JsonProperty("policySpecificationIds")]
        public string[] PolicySpecificationIds { get; set; }

        [IsFilterable,IsFacetable, IsSearchable]
        [JsonProperty("policySpecificationNames")]
        public string[] PolicySpecificationNames { get; set; }

        [IsSearchable]
        [JsonProperty("sourceCode")]
        public string SourceCode { get; set; }

        [IsFilterable, IsSortable, IsFacetable, IsSearchable]
        [JsonProperty("status")]
        public string Status { get; set; }

        [IsFilterable, IsSortable, IsFacetable, IsSearchable]
        [JsonProperty("fundingStreamName")]
        public string FundingStreamName { get; set; }

        [IsFilterable]
        [JsonProperty("fundingStreamId")]
        public string FundingStreamId { get; set; }

        [IsFilterable, IsSortable]
        [JsonProperty("lastUpdatedDate")]
        public DateTimeOffset LastUpdatedDate { get; set; }
    }
}