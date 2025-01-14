﻿using System.Collections.Generic;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Examples;

namespace CalculateFunding.Api.External.V1.Models.Examples
{
    public class FundingStreamExamples : IExamplesProvider
    {
        public object GetExamples()
        {
            FundingStream[] fundingStreams = JsonConvert.DeserializeObject<FundingStream[]>(Properties.Resources.V1_Sample_FundingStreams);

            return fundingStreams;
        }
    }
}