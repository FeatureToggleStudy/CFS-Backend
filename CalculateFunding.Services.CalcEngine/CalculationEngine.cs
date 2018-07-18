﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Results;
using CalculateFunding.Services.Calculator.Interfaces;
using CalculateFunding.Services.Core.Helpers;
using Serilog;
using CalculationResult = CalculateFunding.Models.Results.CalculationResult;

namespace CalculateFunding.Services.Calculator
{
    public class CalculationEngine : ICalculationEngine
    {
        private readonly IAllocationFactory _allocationFactory;
        private readonly ICalculationsRepository _calculationsRepository;
        private readonly ILogger _logger;

        public CalculationEngine(IAllocationFactory allocationFactory, ICalculationsRepository calculationsRepository, ILogger logger)
        {
            Guard.ArgumentNotNull(allocationFactory, nameof(allocationFactory));
            Guard.ArgumentNotNull(calculationsRepository, nameof(calculationsRepository));
            Guard.ArgumentNotNull(logger, nameof(logger));

            _allocationFactory = allocationFactory;
            _calculationsRepository = calculationsRepository;
            _logger = logger;
        }

        public IAllocationModel GenerateAllocationModel(BuildProject buildProject)
        {
            Assembly assembly = Assembly.Load(Convert.FromBase64String(buildProject.Build.AssemblyBase64));

            return _allocationFactory.CreateAllocationModel(assembly);
        }

        async public Task<IEnumerable<ProviderResult>> GenerateAllocations(BuildProject buildProject, IEnumerable<ProviderSummary> providers, Func<string, string, Task<IEnumerable<ProviderSourceDatasetCurrent>>> getProviderSourceDatasets)
        {
            var assembly = Assembly.Load(Convert.FromBase64String(buildProject.Build.AssemblyBase64));

            var allocationModel = _allocationFactory.CreateAllocationModel(assembly);

            IList<ProviderResult> providerResults = new List<ProviderResult>();

            IEnumerable<CalculationSummaryModel> calculations = await _calculationsRepository.GetCalculationSummariesForSpecification(buildProject.SpecificationId);

            Parallel.ForEach(providers, new ParallelOptions { MaxDegreeOfParallelism = 5 }, provider =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                IEnumerable<ProviderSourceDatasetCurrent> providerSourceDatasets = getProviderSourceDatasets(provider.Id, buildProject.SpecificationId).Result;

                if (providerSourceDatasets == null)
                {
                    providerSourceDatasets = Enumerable.Empty<ProviderSourceDatasetCurrent>();
                }

                var result = CalculateProviderResults(allocationModel, buildProject, calculations, provider, providerSourceDatasets.ToList());

                providerResults.Add(result);

                stopwatch.Stop();
                _logger.Debug($"Generated result for {provider.Name} in {stopwatch.ElapsedMilliseconds}ms");
            });

            return providerResults;
        }

        public ProviderResult CalculateProviderResults(IAllocationModel model, BuildProject buildProject, IEnumerable<CalculationSummaryModel> calculations, ProviderSummary provider, IEnumerable<ProviderSourceDatasetCurrent> providerSourceDatasets)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            IEnumerable<CalculationResult> calculationResults;
            try
            {
                calculationResults = model.Execute(providerSourceDatasets != null ? providerSourceDatasets.ToList() : new List<ProviderSourceDatasetCurrent>()).ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }

            var providerCalResults = calculationResults.ToDictionary(x => x.Calculation?.Id);
            stopwatch.Stop();

            if(providerCalResults.Count > 0)
            {
                _logger.Debug($"{providerCalResults.Count} calcs in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / providerCalResults.Count: 0.0000}ms)");
            }
            else
            {
                _logger.Information("There are no calculations to executed for specification ID {specificationId}", buildProject.SpecificationId);
            }

            ProviderResult providerResult = new ProviderResult
            {
                Provider = provider,
                SpecificationId = buildProject.SpecificationId
            };

            byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes($"{providerResult.Provider.Id}-{providerResult.SpecificationId}");
            providerResult.Id = Convert.ToBase64String(plainTextBytes);

            List<CalculationResult> results = new List<CalculationResult>();

            if (calculations != null)
            {
                foreach (CalculationSummaryModel calculation in calculations)
                {
                    CalculationResult result = new CalculationResult
                    {
                        Calculation = calculation.GetReference(),
                        CalculationType = calculation.CalculationType
                    };

                    if (providerCalResults.TryGetValue(calculation.Id, out CalculationResult calculationResult))
                    {
                        result.CalculationSpecification = calculationResult.CalculationSpecification;
                        if (calculationResult.AllocationLine != null)
                            result.AllocationLine = calculationResult.AllocationLine;

                        result.PolicySpecifications = calculationResult.PolicySpecifications;
                        if (calculationResult.Value != decimal.MinValue)
                        {
                            result.Value = calculationResult.Value;
                        }
                        result.Exception = calculationResult.Exception;
                    }

                    results.Add(result);
                }
            }

            providerResult.CalculationResults = results.ToList();

            providerResult.AllocationLineResults = results.Where(x => x.CalculationType == CalculationType.Funding && x.AllocationLine != null)
                .GroupBy(x => x.AllocationLine).Select(x => new AllocationLineResult
                {
                    AllocationLine = x.Key,
                    Value = x.Sum(v => v.Value ?? decimal.Zero)
                }).ToList();

            return providerResult;
        }
    }
}