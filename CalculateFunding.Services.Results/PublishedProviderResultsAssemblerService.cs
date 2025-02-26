﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Results;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Results.Interfaces;
using Serilog;

namespace CalculateFunding.Services.Results
{
    public class PublishedProviderResultsAssemblerService : IPublishedProviderResultsAssemblerService
    {
        private readonly ISpecificationsRepository _specificationsRepository;
        private readonly ILogger _logger;
        private readonly IVersionRepository<PublishedAllocationLineResultVersion> _allocationResultsVersionRepository;
        private readonly IMapper _mapper;

        public PublishedProviderResultsAssemblerService(
            ISpecificationsRepository specificationsRepository,
            ILogger logger,
            IVersionRepository<PublishedAllocationLineResultVersion> allocationResultsVersionRepository,
            IMapper mapper)
        {
            Guard.ArgumentNotNull(specificationsRepository, nameof(specificationsRepository));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(allocationResultsVersionRepository, nameof(allocationResultsVersionRepository));
            Guard.ArgumentNotNull(mapper, nameof(mapper));

            _specificationsRepository = specificationsRepository;
            _logger = logger;
            _allocationResultsVersionRepository = allocationResultsVersionRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<PublishedProviderResult>> AssemblePublishedProviderResults(IEnumerable<ProviderResult> providerResults, Reference author, SpecificationCurrentVersion specificationCurrentVersion)
        {
            Guard.ArgumentNotNull(providerResults, nameof(providerResults));
            Guard.ArgumentNotNull(author, nameof(author));
            Guard.ArgumentNotNull(specificationCurrentVersion, nameof(specificationCurrentVersion));

            string specificationId = specificationCurrentVersion.Id;

            Period fundingPeriod = await _specificationsRepository.GetFundingPeriodById(specificationCurrentVersion.FundingPeriod.Id);

            if (fundingPeriod == null)
            {
                throw new NonRetriableException($"Failed to find a funding period for id: {specificationCurrentVersion.FundingPeriod.Id}");
            }

            IEnumerable<string> providerIds = providerResults.Select(m => m.Provider.Id);

            ConcurrentBag<PublishedProviderResult> publishedProviderResults = new ConcurrentBag<PublishedProviderResult>();

            IEnumerable<FundingStream> allFundingStreams = await GetAllFundingStreams();

            Parallel.ForEach(providerResults, (providerResult) =>
            {
                IEnumerable<PublishedFundingStreamResult> publishedFundingStreamResults = AssembleFundingStreamResults(providerResult, specificationCurrentVersion, author, allFundingStreams);

                foreach (PublishedFundingStreamResult publishedFundingStreamResult in publishedFundingStreamResults)
                {
                    PublishedProviderResult publishedProviderResult = new PublishedProviderResult
                    {
                        ProviderId = providerResult.Provider.Id,
                        SpecificationId = specificationId,
                        FundingStreamResult = publishedFundingStreamResult,
                        FundingPeriod = fundingPeriod,
                    };

                    publishedProviderResult.FundingStreamResult.AllocationLineResult.Current.PublishedProviderResultId = publishedProviderResult.Id;

                    publishedProviderResults.Add(publishedProviderResult);
                }

            });

            return publishedProviderResults;
        }


        public async Task<(IEnumerable<PublishedProviderResult>, IEnumerable<PublishedProviderResultExisting>)> GeneratePublishedProviderResultsToSave(IEnumerable<PublishedProviderResult> providerResults, IEnumerable<PublishedProviderResultExisting> existingResults)
        {
            Guard.ArgumentNotNull(providerResults, nameof(providerResults));
            Guard.ArgumentNotNull(existingResults, nameof(existingResults));

            ConcurrentBag<PublishedProviderResult> publishedProviderResultsToSave = new ConcurrentBag<PublishedProviderResult>();

            ConcurrentDictionary<string, ConcurrentDictionary<string, PublishedProviderResultExisting>> existingProviderResults = new ConcurrentDictionary<string, ConcurrentDictionary<string, PublishedProviderResultExisting>>();

            foreach (PublishedProviderResultExisting providerResult in existingResults)
            {
                if (!existingProviderResults.ContainsKey(providerResult.ProviderId))
                {
                    existingProviderResults.TryAdd(providerResult.ProviderId, new ConcurrentDictionary<string, PublishedProviderResultExisting>());
                }

                existingProviderResults[providerResult.ProviderId].TryAdd(providerResult.AllocationLineId, providerResult);
            }

            List<Task> allTasks = new List<Task>();

            SemaphoreSlim throttler = new SemaphoreSlim(initialCount: 30);

            foreach (PublishedProviderResult providerResult in providerResults)
            {
                await throttler.WaitAsync();
                allTasks.Add(
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (existingProviderResults.ContainsKey(providerResult.ProviderId))
                            {
                                ConcurrentDictionary<string, PublishedProviderResultExisting> existingResultsForProvider = existingProviderResults[providerResult.ProviderId];

                                if (existingResultsForProvider.TryGetValue(providerResult.FundingStreamResult.AllocationLineResult.AllocationLine.Id, out PublishedProviderResultExisting existingResult))
                                {
                                    existingResultsForProvider.TryRemove(providerResult.FundingStreamResult.AllocationLineResult.AllocationLine.Id, out var removedExistingResult);

                                    if (!existingResultsForProvider.Any())
                                    {
                                        existingProviderResults.TryRemove(providerResult.ProviderId, out var removedProviderResult);
                                    }

                                    if (providerResult.FundingStreamResult.AllocationLineResult.Current.Value == existingResult.Value)
                                    {
                                        existingProviderResults.TryRemove(providerResult.ProviderId, out var removedProviderResult);

                                        return;
                                    }

                                    providerResult.FundingStreamResult.AllocationLineResult.Current.Version =
                                        await _allocationResultsVersionRepository.GetNextVersionNumber(providerResult.FundingStreamResult.AllocationLineResult.Current, existingResult.Version, incrementFromCurrentVersion: true);

                                    providerResult.FundingStreamResult.AllocationLineResult.HasResultBeenVaried = existingResult.HasResultBeenVaried;

                                    if (existingResult.Status != AllocationLineStatus.Held)
                                    {
                                        providerResult.FundingStreamResult.AllocationLineResult.Current.Status = AllocationLineStatus.Updated;
                                    }

                                    if (existingResult.Published != null)
                                    {
                                        providerResult.FundingStreamResult.AllocationLineResult.Published = existingResult.Published;
                                    }

                                    providerResult.FundingStreamResult.AllocationLineResult.Current.Major = existingResult.Major;
                                    providerResult.FundingStreamResult.AllocationLineResult.Current.Minor = existingResult.Minor;
                                }
                                else
                                {
                                    providerResult.FundingStreamResult.AllocationLineResult.Current.Version = 1;
                                }
                            }
                            else
                            {
                                providerResult.FundingStreamResult.AllocationLineResult.Current.Version = 1;
                            }

                            publishedProviderResultsToSave.Add(providerResult);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }));
            }
            await TaskHelper.WhenAllAndThrow(allTasks.ToArray());

            // Need to remove results that have been varied
            List<PublishedProviderResult> publishedProviderResultsToSaveList = publishedProviderResultsToSave.ToList();
            publishedProviderResultsToSaveList.RemoveAll(r => r.FundingStreamResult.AllocationLineResult.HasResultBeenVaried);

            List<PublishedProviderResultExisting> existingRecordsExclude = new List<PublishedProviderResultExisting>(existingProviderResults.Values.Count);
            foreach (ConcurrentDictionary<string, PublishedProviderResultExisting> existingList in existingProviderResults.Values)
            {
                existingRecordsExclude.AddRange(existingList.Values.Where(r => !r.HasResultBeenVaried));
            }

            return (publishedProviderResultsToSaveList, existingRecordsExclude);
        }

        private (Policy policy, Policy parentPolicy, Calculation calculation) FindPolicy(string calculationSpecificationId, IEnumerable<Policy> policies)
        {
            foreach (Policy policy in policies)
            {
                if (policy != null)
                {
                    if (policy.Calculations != null)
                    {
                        Calculation calc = policy.Calculations.FirstOrDefault(c => c.Id == calculationSpecificationId);
                        if (calc != null)
                        {
                            return (policy, null, calc);
                        }
                    }

                    if (policy.SubPolicies != null)
                    {
                        foreach (Policy subpolicy in policy.SubPolicies)
                        {
                            Calculation calc = subpolicy.Calculations.FirstOrDefault(c => c.Id == calculationSpecificationId);

                            if (subpolicy.Calculations.Any(c => c.Id == calculationSpecificationId))
                            {
                                return (subpolicy, policy, calc);
                            }
                        }
                    }
                }
            }

            return (null, null, null);
        }

        private PublishedCalculationType ConvertCalculationType(Models.Calcs.CalculationType calculationType)
        {
            switch (calculationType)
            {
                case Models.Calcs.CalculationType.Funding:
                    return PublishedCalculationType.Funding;
                case Models.Calcs.CalculationType.Number:
                    return PublishedCalculationType.Number;
                case Models.Calcs.CalculationType.Baseline:
                    return PublishedCalculationType.Baseline;
                default:
                    throw new NonRetriableException($"Unknown CalculationType: {calculationType}");
            }
        }

        private IEnumerable<PublishedFundingStreamResult> AssembleFundingStreamResults(ProviderResult providerResult, SpecificationCurrentVersion specificationCurrentVersion, Reference author, IEnumerable<FundingStream> allFundingStreams)
        {
            IList<PublishedFundingStreamResult> publishedFundingStreamResults = new List<PublishedFundingStreamResult>();

            Dictionary<string, PublishedAllocationLineDefinition> publishedAllocationLines = new Dictionary<string, PublishedAllocationLineDefinition>();

            foreach (Reference fundingStreamReference in specificationCurrentVersion.FundingStreams)
            {
                FundingStream fundingStream = allFundingStreams.FirstOrDefault(m => m.Id == fundingStreamReference.Id);

                if (fundingStream == null)
                {
                    throw new NonRetriableException($"Failed to find a funding stream for id: {fundingStreamReference.Id}");
                }

                PublishedFundingStreamDefinition publishedFundingStreamDefinition = _mapper.Map<PublishedFundingStreamDefinition>(fundingStream);

                List<PublishedProviderCalculationResult> publishedProviderCalculationResults = new List<PublishedProviderCalculationResult>(providerResult.CalculationResults.Count());

                foreach (CalculationResult calculationResult in providerResult.CalculationResults)
                {
                    (Policy policy, Policy parentPolicy, Calculation calculation) = FindPolicy(calculationResult.CalculationSpecification?.Id, specificationCurrentVersion.Policies);

                    if (calculation == null)
                    {
                        throw new NonRetriableException($"Calculation specification not found in specification. Calculation Spec Id ='{calculationResult?.CalculationSpecification?.Id}'");
                    }

                    if (calculation.CalculationType == CalculationType.Number && !calculation.IsPublic)
                    {
                        continue;
                    }

                    PublishedProviderCalculationResult publishedProviderCalculationResult = new PublishedProviderCalculationResult()
                    {
                        CalculationSpecification = calculationResult.CalculationSpecification,
                        AllocationLine = calculationResult.AllocationLine,
                        IsPublic = calculation.IsPublic,
                        CalculationType = ConvertCalculationType(calculationResult.CalculationType),
                        Value = calculationResult.Value,
                        CalculationVersion = calculationResult.Version
                    };

                    if (policy != null)
                    {
                        publishedProviderCalculationResult.Policy = new PolicySummary(policy.Id, policy.Name, policy.Description);
                    }

                    if (parentPolicy != null)
                    {
                        publishedProviderCalculationResult.ParentPolicy = new PolicySummary(parentPolicy.Id, parentPolicy.Name, parentPolicy.Description);
                    }

                    publishedProviderCalculationResults.Add(publishedProviderCalculationResult);
                }

                IEnumerable<IGrouping<string, CalculationResult>> allocationLineGroups = providerResult
                    .CalculationResults
                    .Where(c => c.CalculationType == Models.Calcs.CalculationType.Funding && c.Value.HasValue && c.AllocationLine != null && !string.IsNullOrWhiteSpace(c.AllocationLine.Id))
                    .GroupBy(m => m.AllocationLine.Id);

                foreach (IGrouping<string, CalculationResult> allocationLineResultGroup in allocationLineGroups)
                {
                    PublishedAllocationLineDefinition publishedAllocationLine;
                    if (!publishedAllocationLines.TryGetValue(allocationLineResultGroup.Key, out publishedAllocationLine))
                    {
                        AllocationLine allocationLine = fundingStream.AllocationLines.FirstOrDefault(m => m.Id == allocationLineResultGroup.Key);
                        if (allocationLine != null)
                        {
                            publishedAllocationLine = _mapper.Map<PublishedAllocationLineDefinition>(allocationLine);
                            publishedAllocationLines.Add(allocationLineResultGroup.Key, publishedAllocationLine);
                        }
                    }

                    if (publishedAllocationLine != null)
                    {
                        PublishedFundingStreamResult publishedFundingStreamResult = new PublishedFundingStreamResult
                        {
                            FundingStream = publishedFundingStreamDefinition,

                            FundingStreamPeriod = $"{fundingStream.Id}{specificationCurrentVersion.FundingPeriod.Id}",

                            DistributionPeriod = $"{fundingStream.PeriodType.Id}{specificationCurrentVersion.FundingPeriod.Id}"
                        };

                        PublishedAllocationLineResultVersion publishedAllocationLineResultVersion = new PublishedAllocationLineResultVersion
                        {
                            Author = author,
                            Date = DateTimeOffset.Now,
                            Status = AllocationLineStatus.Held,
                            Value = allocationLineResultGroup.Sum(m => m.Value),
                            Provider = providerResult.Provider,
                            SpecificationId = specificationCurrentVersion.Id,
                            ProviderId = providerResult.Provider.Id,
                            Calculations = publishedProviderCalculationResults.Where(c => c.AllocationLine == null || string.Equals(c.AllocationLine.Id, publishedAllocationLine.Id, StringComparison.InvariantCultureIgnoreCase)),
                        };

                        publishedFundingStreamResult.AllocationLineResult = new PublishedAllocationLineResult
                        {
                            AllocationLine = publishedAllocationLine,
                            Current = publishedAllocationLineResultVersion
                        };

                        publishedFundingStreamResults.Add(publishedFundingStreamResult);
                    }
                }
            }

            return publishedFundingStreamResults;
        }

        private async Task<IEnumerable<FundingStream>> GetAllFundingStreams()
        {

            IEnumerable<FundingStream> allFundingStreams = await _specificationsRepository.GetFundingStreams();

            if (allFundingStreams.IsNullOrEmpty())
            {
                throw new NonRetriableException("Failed to get all funding streams");
            }

            return allFundingStreams;
        }
    }
}
