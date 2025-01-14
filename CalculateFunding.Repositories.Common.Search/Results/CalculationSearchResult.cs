﻿using System;

namespace CalculateFunding.Repositories.Common.Search.Results
{

    public class CalculationSearchResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FundingPeriodName { get; set; }
        public string SpecificationName { get; set; }
        public string Status { get; set; }
        public string CalculationType { get; set;  }
        public DateTimeOffset? LastUpdatedDate { get; set; }
    }
}
