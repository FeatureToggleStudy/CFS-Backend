﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Models;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Results;
using CalculateFunding.Models.Specs;
using CalculateFunding.Repository;
using CalculateFunding.Services.Compiler;
using CalculateFunding.Services.TestRunner;
using CalculateFunding.Services.TestRunner.Vocab;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CalculateFunding.Services.Calculator
{
    public class CalculationEngine
    {
        private readonly Repository<ProviderSourceDataset> _providerSourceRepository;
        private readonly Repository<ProviderResult> _providerResultRepository;
        private readonly Repository<ProviderTestResult> _providerTestResultRepository;
        private readonly ILangugeSyntaxProvider _langugeSyntaxProvider;

        public CalculationEngine(Repository<ProviderSourceDataset> providerSourceRepository, Repository<ProviderResult> providerResultRepository, Repository<ProviderTestResult> providerTestResultRepository, ILangugeSyntaxProvider langugeSyntaxProvider)
        {
            _providerSourceRepository = providerSourceRepository;
            _providerResultRepository = providerResultRepository;
            _providerTestResultRepository = providerTestResultRepository;
            _langugeSyntaxProvider = langugeSyntaxProvider;
        }

        public async Task GenerateAllocations(BudgetCompilerOutput compilerOutput)
        {
            var allocationFactory = new AllocationFactory(compilerOutput.Assembly);

                var datasetsByUrn = _providerSourceRepository.Query().Where(x => x.DocumentType == "ProviderSourceDataset" && x.BudgetId == compilerOutput.Budget.Id).ToArray().GroupBy(x => x.ProviderUrn);

                foreach (var urn in datasetsByUrn)
                {
                    var typedDatasets = new List<object>();

                    string providerName = urn.Key;
                    var datasets = urn.ToArray();
                    foreach (var dataset in datasets)
                    {
                       
                        var type = allocationFactory.GetDatasetType(dataset.DatasetName);
                        var nameField = typeof(ProviderSourceDataset).GetProperty("ProviderName");
                        if (nameField != null)
                        {
                            providerName = nameField.GetValue(dataset)?.ToString();
                        }

                        var datasetAsJson = _providerSourceRepository.QueryAsJson($"SELECT * FROM ds WHERE ds.id='{dataset.Id}' AND ds.deleted = false").First();


                        object blah = JsonConvert.DeserializeObject(datasetAsJson, type, new JsonSerializerSettings{ContractResolver = new CamelCasePropertyNamesContractResolver()});
                        typedDatasets.Add(blah);
                    }

                    var provider = new Reference(urn.Key, providerName);
                    var result = CalculateProviderProducts(allocationFactory, compilerOutput, provider, typedDatasets);
                    var testResult = RunProviderTests(compilerOutput, provider, typedDatasets, result);

                    await _providerTestResultRepository.CreateAsync(testResult);    
                    await _providerResultRepository.CreateAsync(result);
                   
                }
            
        }

        public async Task<List<object>> GetProviderDatasets(AllocationFactory allocationFactory, Reference provider, string budgetId)
        {
            var typedDatasets = new List<object>();

                var datasetsAsJson = _providerSourceRepository.QueryAsJson($"SELECT * FROM ds WHERE ds.budgetId='{budgetId}' AND ds.providerUrn='{provider.Id}' AND ds.deleted = false").ToList();

                foreach (var datasetAsJson in datasetsAsJson)
                {
                    var dataset = JsonConvert.DeserializeObject<ProviderSourceDataset>(datasetAsJson);
                    var type = allocationFactory.GetDatasetType(dataset.DatasetName);

                    object blah = JsonConvert.DeserializeObject(datasetAsJson, type);
                    typedDatasets.Add(blah);
                }            
            
            return typedDatasets;
        }

        public ProviderResult CalculateProviderProducts(AllocationFactory allocationFactory, BudgetCompilerOutput compilerOutput, Reference provider, List<object> typedDatasets)
        {
            var model = allocationFactory.CreateAllocationModel();

            var calculationResults = model.Execute(compilerOutput.Budget.Name, typedDatasets.ToArray());

            var providerAllocations = calculationResults.ToDictionary(x => x.ProductName);

            var result = new ProviderResult
            {
                Provider = provider,
                Budget = new Reference(compilerOutput.Budget.Id, compilerOutput.Budget.Name),
                SourceDatasets = typedDatasets.ToArray()
            };
            var productResults = new List<ProductResult>();

            foreach (var fundingPolicy in compilerOutput.Budget.FundingPolicies)
            {
                foreach (var allocationLine in fundingPolicy.AllocationLines ?? new List<AllocationLine>())
                {
                    foreach (var productFolder in allocationLine.ProductFolders ?? new List<ProductFolder>())
                    {
                        foreach (var product in productFolder.Products)
                        {
                            var productResult = new ProductResult
                            {
                                FundingPolicy = new Reference(fundingPolicy.Id, fundingPolicy.Name),
                                AllocationLine = new Reference(allocationLine.Id, allocationLine.Name),
                                ProductFolder = new Reference(productFolder.Id, productFolder.Name),
                                Product = product
                            };
                            var productIdentifier = _langugeSyntaxProvider.GetIdentitier(product.Name, compilerOutput.Budget.TargetLanguage);
                            if (providerAllocations.ContainsKey(productIdentifier))
                            {
                                var calculationResult = providerAllocations[productIdentifier];
                                productResult.Value = calculationResult.Value;
                                productResult.Exception = calculationResult.Exception;
                            }

                            productResults.Add(productResult);
                        }
                    }
                }
            }
            result.ProductResults = productResults.ToArray();
            return result;
        }

        public ProviderTestResult RunProviderTests(BudgetCompilerOutput compilerOutput, Reference provider, List<object> typedDatasets, ProviderResult providerResult)
        {
            var gherkinExecutor = new GherkinExecutor(new ProductGherkinVocabulary());

            var testResult = new ProviderTestResult
            {
                Provider = provider,
                Budget = new Reference(compilerOutput.Budget.Id, compilerOutput.Budget.Name),
            };
            var scenarioResults = new List<ProductTestScenarioResult>();
            foreach (var productResult in providerResult.ProductResults)
            {

                if (productResult.Product.TestScenarios != null)
                {
                    var gherkinScenarioResults =
                        gherkinExecutor.Execute(productResult, typedDatasets, productResult.Product.TestScenarios);

                    foreach (var executeResult in gherkinScenarioResults)
                    {
                        scenarioResults.Add(new ProductTestScenarioResult
                        {
                            FundingPolicy = productResult.FundingPolicy,
                            AllocationLine = productResult.AllocationLine,
                            ProductFolder = productResult.ProductFolder,
                            Product = productResult.Product,
                            ProductValue = productResult.Value,
                            Scenario = executeResult.Scenario,
                            TestResult =
                                executeResult.StepsExecuted < executeResult.TotalSteps
                                    ? TestResult.Ignored
                                    : executeResult.HasErrors
                                        ? TestResult.Failed
                                        : TestResult.Passed,
                            StepExected = executeResult.StepsExecuted,
                            TotalSteps = executeResult.TotalSteps,
                            DatasetReferences = executeResult.Dependencies.Select(x => new DatasetReference
                            {
                                DatasetName = x.DatasetName,
                                FieldName = x.FieldName,
                                Value = x.Value
                            }).ToArray()
                        });
                    }
                }
            }

            testResult.ScenarioResults = scenarioResults.ToArray();
            return testResult;
        }

    }
}
