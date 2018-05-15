﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CalculateFunding.Services.Core.Caching
{
    public static class CacheKeys
    {
        public const string TestScenarios = "test-scenarios:";

        public const string GherkinParseResult = "gherkin-parse-result:";

        public const string ProviderResultBatch = "provider-results-batch:";

        public const string ScopedProviderSummariesPrefix = "scoped-provider-summaries:";

        public const string FundingPeriods = "funding-periods";

        public static string SpecificationSummaryById { get; set; } = "specification-summary:";

        public static string SpecificationCurrentVersionById { get; set; } = "specification-current-version:";

        public static string SpecificationSummariesByFundingPeriodId { get; set; } = "specification-summaries-funding-period:";

        public static string SpecificationSummaries { get; set; } = "specification-summaries";
    }
}
