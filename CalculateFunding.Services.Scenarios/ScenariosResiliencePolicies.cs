﻿using CalculateFunding.Services.Scenarios.Interfaces;
using Polly;

namespace CalculateFunding.Services.Scenarios
{
    public class ScenariosResiliencePolicies : IScenariosResiliencePolicies
    {
        public Policy CalcsRepository { get; set; }

        public Policy JobsApiClient { get; set; }
    }
}
