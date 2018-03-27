﻿using CalculateFunding.Models.Scenarios;

namespace CalculateFunding.Services.TestRunner
{
    [TestStep("given", "the field '(.*)' in the dataset '(.*)' (.*) (.*)")]
    public class SourceDatasetStep
    {
        [TestStepArgument(StepArgumentType.FieldName)]
        public string FieldName { get; set; }
        [TestStepArgument(StepArgumentType.DatasetName)]
        public string DatasetName { get; set; }
        public ComparisonOperator Operator { get; set; }
        public string Value { get; set; }
    }
}
