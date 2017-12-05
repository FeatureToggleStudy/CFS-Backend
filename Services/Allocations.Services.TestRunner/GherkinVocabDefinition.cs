﻿using Allocations.Models.Specs;

namespace Allocations.Services.TestRunner
{
    public abstract class GherkinVocabDefinition
    {
        protected GherkinVocabDefinition(params GherkinStepAction[] stepsAction)
        {
            StepsAction = stepsAction;
        }

        public GherkinStepAction GetAction(TestStepType stepType)
        {
            foreach (var stepAction in StepsAction)
            {
                if (stepAction.IsMatch(stepType))
                {
                    return stepAction;
                }
            }
            return null;
        }

        protected GherkinStepAction[] StepsAction { get; }
    }
}