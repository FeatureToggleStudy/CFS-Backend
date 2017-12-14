using System.Collections.Generic;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Results;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Compiler;

namespace CalculateFunding.Functions.Calcs.Models
{
    public class PreviewResponse
    {
        public PreviewResponse()
        {
            TestResults = new List<ProviderTestResult>();
        }
        public CalculationImplementation Product { get; set; }
        public CompilerOutput CompilerOutput { get; set; }
        public List<ProviderTestResult> TestResults { get; set; }
    }
}