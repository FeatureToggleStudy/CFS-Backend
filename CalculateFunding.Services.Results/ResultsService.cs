using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.FeatureToggles;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Models.Results;
using CalculateFunding.Models.Results.Search;
using CalculateFunding.Models.Specs;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Interfaces.ServiceBus;
using CalculateFunding.Services.Results.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Search.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.Storage.Blob;
using Serilog;

namespace CalculateFunding.Services.Results
{
    public class ResultsService : IResultsService, IHealthChecker
    {
        private readonly ILogger _logger;
        private readonly ITelemetry _telemetry;
        private readonly ICalculationResultsRepository _resultsRepository;
        private readonly IMapper _mapper;
        private readonly ISearchRepository<ProviderIndex> _searchRepository;
        private readonly IProviderSourceDatasetRepository _providerSourceDatasetRepository;
        private readonly ISearchRepository<ProviderCalculationResultsIndex> _calculationProviderResultsSearchRepository;
        private readonly Polly.Policy _resultsRepositoryPolicy;
        private readonly ISpecificationsRepository _specificationsRepository;
        private readonly Polly.Policy _resultsSearchRepositoryPolicy;
        private readonly Polly.Policy _specificationsRepositoryPolicy;
        private readonly IProviderImportMappingService _providerImportMappingService;
        private readonly ICacheProvider _cacheProvider;
        private readonly IMessengerService _messengerService;
        private readonly ICalculationsRepository _calculationRepository;
        private readonly Polly.Policy _calculationsRepositoryPolicy;
        private readonly IValidator<MasterProviderModel> _masterProviderModelValidator;
        private readonly IFeatureToggle _featureToggle;
        private readonly IBlobClient _blobClient;
        private readonly Polly.Policy _blobClientPolicy;

        public ResultsService(ILogger logger,
            IFeatureToggle featureToggle,
            ICalculationResultsRepository resultsRepository,
            IMapper mapper,
            ISearchRepository<ProviderIndex> searchRepository,
            ITelemetry telemetry,
            IProviderSourceDatasetRepository providerSourceDatasetRepository,
            ISearchRepository<ProviderCalculationResultsIndex> calculationProviderResultsSearchRepository,
            ISpecificationsRepository specificationsRepository,
            IResultsResiliencePolicies resiliencePolicies,
            IProviderImportMappingService providerImportMappingService,
            ICacheProvider cacheProvider,
            IMessengerService messengerService,
            ICalculationsRepository calculationRepository,
            IValidator<MasterProviderModel> masterProviderModelValidator,
            IBlobClient blobClient)
        {
            Guard.ArgumentNotNull(resultsRepository, nameof(resultsRepository));
            Guard.ArgumentNotNull(mapper, nameof(mapper));
            Guard.ArgumentNotNull(searchRepository, nameof(searchRepository));
            Guard.ArgumentNotNull(telemetry, nameof(telemetry));
            Guard.ArgumentNotNull(providerSourceDatasetRepository, nameof(providerSourceDatasetRepository));
            Guard.ArgumentNotNull(calculationProviderResultsSearchRepository, nameof(calculationProviderResultsSearchRepository));
            Guard.ArgumentNotNull(specificationsRepository, nameof(specificationsRepository));
            Guard.ArgumentNotNull(resiliencePolicies, nameof(resiliencePolicies));
            Guard.ArgumentNotNull(providerImportMappingService, nameof(providerImportMappingService));
            Guard.ArgumentNotNull(cacheProvider, nameof(cacheProvider));
            Guard.ArgumentNotNull(messengerService, nameof(messengerService));
            Guard.ArgumentNotNull(calculationRepository, nameof(calculationRepository));
            Guard.ArgumentNotNull(masterProviderModelValidator, nameof(masterProviderModelValidator));
            Guard.ArgumentNotNull(featureToggle, nameof(featureToggle));
            Guard.ArgumentNotNull(blobClient, nameof(blobClient));

            _logger = logger;
            _resultsRepository = resultsRepository;
            _mapper = mapper;
            _searchRepository = searchRepository;
            _telemetry = telemetry;
            _providerSourceDatasetRepository = providerSourceDatasetRepository;
            _calculationProviderResultsSearchRepository = calculationProviderResultsSearchRepository;
            _resultsRepositoryPolicy = resiliencePolicies.ResultsRepository;
            _specificationsRepository = specificationsRepository;
            _resultsSearchRepositoryPolicy = resiliencePolicies.ResultsSearchRepository;
            _specificationsRepositoryPolicy = resiliencePolicies.SpecificationsRepository;
            _providerImportMappingService = providerImportMappingService;
            _cacheProvider = cacheProvider;
            _messengerService = messengerService;
            _calculationRepository = calculationRepository;
            _masterProviderModelValidator = masterProviderModelValidator;
            _calculationsRepositoryPolicy = resiliencePolicies.CalculationsRepository;
            _featureToggle = featureToggle;
            _blobClient = blobClient;
            _blobClientPolicy = resiliencePolicies.CsvBlobPolicy;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            ServiceHealth datasetsRepoHealth = await ((IHealthChecker)_resultsRepository).IsHealthOk();
            (bool Ok, string Message) searchRepoHealth = await _searchRepository.IsHealthOk();
            ServiceHealth providerSourceDatasetRepoHealth = await ((IHealthChecker)_providerSourceDatasetRepository).IsHealthOk();
            (bool Ok, string Message) calcSearchRepoHealth = await _calculationProviderResultsSearchRepository.IsHealthOk();

            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ResultsService)
            };
            health.Dependencies.AddRange(datasetsRepoHealth.Dependencies);
            health.Dependencies.Add(new DependencyHealth { HealthOk = searchRepoHealth.Ok, DependencyName = _searchRepository.GetType().GetFriendlyName(), Message = searchRepoHealth.Message });
            health.Dependencies.AddRange(providerSourceDatasetRepoHealth.Dependencies);
            health.Dependencies.Add(new DependencyHealth { HealthOk = calcSearchRepoHealth.Ok, DependencyName = _calculationProviderResultsSearchRepository.GetType().GetFriendlyName(), Message = calcSearchRepoHealth.Message });

            return health;
        }

        public async Task<IActionResult> GetProviderById(HttpRequest request)
        {
            string providerId = request.GetParameter("providerId");

            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.Error("No provider Id was provided to GetProviderById");
                return new BadRequestObjectResult("Null or empty provider Id provided");
            }

            ProviderIndex provider = await _resultsRepositoryPolicy.ExecuteAsync(() => _searchRepository.SearchById(providerId, IdFieldOverride: "providerId"));

            if (provider == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(provider);
        }

        public async Task<IActionResult> GetProviderResults(HttpRequest request)
        {
            string providerId = request.GetParameter("providerId");
            string specificationId = request.GetParameter("specificationId");

            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.Error("No provider Id was provided to GetProviderResults");
                return new BadRequestObjectResult("Null or empty provider Id provided");
            }

            if (string.IsNullOrWhiteSpace(specificationId))
            {
                _logger.Error("No specification Id was provided to GetProviderResults");
                return new BadRequestObjectResult("Null or empty specification Id provided");
            }

            ProviderResult providerResult = await _resultsRepositoryPolicy.ExecuteAsync(() => _resultsRepository.GetProviderResult(providerId, specificationId));

            if (providerResult != null)
            {
                _logger.Information($"A result was found for provider id {providerId}, specification id {specificationId}");

                return new OkObjectResult(providerResult);
            }

            _logger.Information($"A result was not found for provider id {providerId}, specification id {specificationId}");

            return new NotFoundResult();
        }

        #region "GetProviderResultsBySpecificationId"
        public async Task<IActionResult> GetProviderResultsBySpecificationId(HttpRequest request)
        {
            string specificationId = request.GetParameter("specificationId");

            if (string.IsNullOrWhiteSpace(specificationId))
            {
                _logger.Error("No specification Id was provided to GetProviderResults");
                return new BadRequestObjectResult("Null or empty specification Id provided");
            }

            string top = request.GetParameter("top");

            return new OkObjectResult(await ProviderResultsBySpecificationId(specificationId, top));
        }

        public async Task<IEnumerable<ProviderResult>> ProviderResultsBySpecificationId(string specificationId, string top)
        {
            IEnumerable<ProviderResult> providerResults = null;

            if (!string.IsNullOrWhiteSpace(top))
            {
                if (int.TryParse(top, out int maxResults))
                {
                    providerResults = await ProviderResultsBySpecificationId(specificationId, maxResults);

                    return providerResults;
                }
            }

            providerResults = await ProviderResultsBySpecificationId(specificationId);

            return providerResults;
        }

        public async Task<IEnumerable<ProviderResult>> ProviderResultsBySpecificationId(string specificationId, int maxResults = -1)
        {
            return await _resultsRepositoryPolicy.ExecuteAsync(() => _resultsRepository.GetProviderResultsBySpecificationId(specificationId, maxResults));
        }
        #endregion

        #region "GetProviderSpecifications"
        /// <summary>
        /// Returns distinct specificationIds where there are results for this provider
        /// </summary>
        public async Task<IActionResult> GetProviderSpecifications(HttpRequest request)
        {
            string providerId = request.GetParameter("providerId");
            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.Error("No provider Id was provided to GetProviderSpecifications");
                return new BadRequestObjectResult("Null or empty provider Id provided");
            }

            return await GetProviderSpecifications(providerId);
        }

        /// <summary>
        /// Returns distinct specificationIds where there are results for this provider
        /// </summary>
        /// <param name="providerId"></param>
        /// <returns></returns>
        public async Task<IActionResult> GetProviderSpecifications(string providerId)
        {
            List<string> result = new List<string>();

            IEnumerable<ProviderResult> providerResults = (await _resultsRepositoryPolicy.ExecuteAsync(() => _resultsRepository.GetSpecificationResults(providerId))).ToList();

            if (!providerResults.IsNullOrEmpty())
            {
                _logger.Information($"Results was found for provider id '{providerId}'");

                result.AddRange(providerResults
                    .Where(m => !string.IsNullOrWhiteSpace(m.SpecificationId))
                    .Select(s => s.SpecificationId)
                    .Distinct());
            }
            else
            {
                _logger.Information($"Results were not found for provider id '{providerId}'");
            }

            return new OkObjectResult(result);
        }
        #endregion

        public async Task<IActionResult> GetFundingCalculationResultsForSpecifications(HttpRequest request)
        {
            SpecificationListModel specifications = await request.ReadBodyJson<SpecificationListModel>();

            if (specifications == null)
            {
                _logger.Error("Null specification model provided");

                return new BadRequestObjectResult("Null specifications model provided");
            }

            if (specifications.SpecificationIds.IsNullOrEmpty())
            {
                _logger.Error("Null or empty specification ids provided");

                return new BadRequestObjectResult("Null or empty specification ids provided");
            }

            ConcurrentBag<FundingCalculationResultsTotals> totalsModels = new ConcurrentBag<FundingCalculationResultsTotals>();

            IList<Task> totalsTasks = new List<Task>();

            foreach (string specificationId in specifications.SpecificationIds)
            {
                totalsTasks.Add(Task.Run(async () =>
                {
                    decimal totalResult = await _resultsRepository.GetCalculationResultTotalForSpecificationId(specificationId);

                    totalsModels.Add(new FundingCalculationResultsTotals
                    {
                        SpecificationId = specificationId,
                        TotalResult = totalResult
                    });

                }));
            }

            try
            {
                await TaskHelper.WhenAllAndThrow(totalsTasks.ToArray());
            }
            catch (Exception ex)
            {
                return new InternalServerErrorResult($"An error occurred when obtaining calculation totals with the following message: \n {ex.Message}");
            }

            return new OkObjectResult(totalsModels);
        }

        public async Task<IActionResult> GetProviderSourceDatasetsByProviderIdAndSpecificationId(HttpRequest request)
        {
            string specificationId = request.GetParameter("specificationId");

            if (string.IsNullOrWhiteSpace(specificationId))
            {
                _logger.Error("No specification Id was provided to GetProviderResultsBySpecificationId");
                return new BadRequestObjectResult("Null or empty specification Id provided");
            }

            string providerId = request.GetParameter("providerId");

            if (string.IsNullOrWhiteSpace(providerId))
            {
                _logger.Error("No provider Id was provided to GetProviderResultsBySpecificationId");
                return new BadRequestObjectResult("Null or empty provider Id provided");
            }

            IEnumerable<ProviderSourceDataset> providerResults = await _resultsRepositoryPolicy.ExecuteAsync(() => _providerSourceDatasetRepository.GetProviderSourceDatasets(providerId, specificationId));

            return new OkObjectResult(providerResults);
        }

        public async Task<IActionResult> GetScopedProviderIdsBySpecificationId(HttpRequest request)
        {
            string specificationId = request.GetParameter("specificationId");

            if (string.IsNullOrWhiteSpace(specificationId))
            {
                _logger.Error("No specification Id was provided to GetProviderResultsBySpecificationId");
                return new BadRequestObjectResult("Null or empty specification Id provided");
            }

            IEnumerable<string> providerResults = (await _resultsRepositoryPolicy.ExecuteAsync(() => _providerSourceDatasetRepository.GetAllScopedProviderIdsForSpecificationId(specificationId))).ToList();

            return new OkObjectResult(providerResults);
        }

        public async Task<IActionResult> ReIndexCalculationProviderResults()
        {
            IEnumerable<DocumentEntity<ProviderResult>> providerResults = await _resultsRepositoryPolicy.ExecuteAsync(() => _resultsRepository.GetAllProviderResults());

            IList<ProviderCalculationResultsIndex> searchItems = new List<ProviderCalculationResultsIndex>();

            Dictionary<string, SpecificationSummary> specifications = new Dictionary<string, SpecificationSummary>();

            foreach (DocumentEntity<ProviderResult> documentEntity in providerResults)
            {
                ProviderResult providerResult = documentEntity.Content;

                foreach (CalculationResult calculationResult in providerResult.CalculationResults)
                {
                    SpecificationSummary specificationSummary = null;
                    if (!specifications.ContainsKey(providerResult.SpecificationId))
                    {
                        specificationSummary = await _specificationsRepositoryPolicy.ExecuteAsync(() => _specificationsRepository.GetSpecificationSummaryById(providerResult.SpecificationId));
                        if (specificationSummary == null)
                        {
                            throw new InvalidOperationException($"Specification Summary returned null for specification ID '{providerResult.SpecificationId}'");
                        }

                        specifications.Add(providerResult.SpecificationId, specificationSummary);
                    }
                    else
                    {
                        specificationSummary = specifications[providerResult.SpecificationId];
                    }

                    ProviderCalculationResultsIndex searchItem = new ProviderCalculationResultsIndex
                    {
                        SpecificationId = providerResult.SpecificationId,
                        SpecificationName = specificationSummary?.Name,
                        CalculationName = providerResult.CalculationResults.Select(x => x.Calculation.Name).ToArraySafe(),
                        CalculationId = providerResult.CalculationResults.Select(x => x.Calculation.Id).ToArraySafe(),
                        ProviderId = providerResult.Provider.Id,
                        ProviderName = providerResult.Provider.Name,
                        ProviderType = providerResult.Provider.ProviderType,
                        ProviderSubType = providerResult.Provider.ProviderSubType,
                        LocalAuthority = providerResult.Provider.Authority,
                        LastUpdatedDate = documentEntity.UpdatedAt,
                        UKPRN = providerResult.Provider.UKPRN,
                        URN = providerResult.Provider.URN,
                        UPIN = providerResult.Provider.UPIN,
                        EstablishmentNumber = providerResult.Provider.EstablishmentNumber,
                        OpenDate = providerResult.Provider.DateOpened,
                        CalculationResult = providerResult.CalculationResults.Select(m => m.Value.HasValue ? m.Value.ToString() : "null").ToArraySafe()
                    };

                    if (_featureToggle.IsExceptionMessagesEnabled())
                    {
                        searchItem.CalculationException = providerResult.CalculationResults
                            .Where(m => !string.IsNullOrWhiteSpace(m.ExceptionType))
                            .Select(e => e.Calculation.Id)
                            .ToArraySafe();

                        searchItem.CalculationExceptionType = providerResult.CalculationResults
                            .Select(m => m.ExceptionType ?? string.Empty)
                            .ToArraySafe();

                        searchItem.CalculationExceptionMessage = providerResult.CalculationResults
                            .Select(m => m.ExceptionMessage ?? string.Empty)
                            .ToArraySafe();
                    }

                    searchItems.Add(searchItem);
                }
            }

            const int partitionSize = 500;
            for (int i = 0; i < searchItems.Count; i += partitionSize)
            {
                IEnumerable<ProviderCalculationResultsIndex> partitionedResults = searchItems.Skip(i).Take(partitionSize);

                IEnumerable<IndexError> errors = await _resultsSearchRepositoryPolicy.ExecuteAsync(() => _calculationProviderResultsSearchRepository.Index(partitionedResults));

                if (errors.Any())
                {
                    _logger.Error($"Failed to index calculation provider result documents with errors: { string.Join(";", errors.Select(m => m.ErrorMessage)) }");

                    return new InternalServerErrorResult(null);
                }
            }

            return new NoContentResult();
        }

        public async Task CleanupProviderResultsForSpecification(Message message)
        {
            string specificationId = message.UserProperties["specificationId"].ToString();

            SpecificationProviders specificationProviders = message.GetPayloadAsInstanceOf<SpecificationProviders>();

            IEnumerable<ProviderResult> providerResults = await _resultsRepositoryPolicy
                .ExecuteAsync(() => _resultsRepository.GetProviderResultsBySpecificationIdAndProviders(specificationProviders.Providers, specificationId)
            );

            if (providerResults.Any())
            {
                _logger.Information($"Removing {specificationProviders.Providers.Count()} from calculation results for specification {specificationId}");

                await _resultsRepositoryPolicy
                    .ExecuteAsync(() => _resultsRepository.DeleteCurrentProviderResults(providerResults)
                );

                SearchResults<ProviderCalculationResultsIndex> indexItems = await _resultsSearchRepositoryPolicy
                    .ExecuteAsync(() => _calculationProviderResultsSearchRepository.Search(string.Empty,
                            new SearchParameters
                            {
                                Top = providerResults.Count(),
                                SearchMode = SearchMode.Any,
                                Filter = $"specificationId eq '{specificationId}' and (" + string.Join(" or ", providerResults.Select(m => $"providerId eq '{m.Provider.Id}'")) + ")",
                                QueryType = QueryType.Full
                            }
                        )
                    );

                await _resultsSearchRepositoryPolicy.ExecuteAsync(() => _calculationProviderResultsSearchRepository.Remove(indexItems?.Results.Select(m => m.Result)));
            }
        }

        public async Task<IActionResult> RemoveCurrentProviders()
        {
            try
            {
                await _searchRepository.DeleteIndex();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete providers index");

                return new InternalServerErrorResult(ex.Message);
            }

            bool cachedSummaryCountExists = await _cacheProvider.KeyExists<string>(CacheKeys.AllProviderSummaryCount);

            if (cachedSummaryCountExists)
            {
                await _cacheProvider.KeyDeleteAsync<string>(CacheKeys.AllProviderSummaryCount);
            }

            bool cachedSummariesExists = await _cacheProvider.KeyExists<ProviderSummary>(CacheKeys.AllProviderSummaries);

            if (cachedSummariesExists)
            {
                await _cacheProvider.RemoveAsync<ProviderSummary>(CacheKeys.AllProviderSummaries);
            }

            await _cacheProvider.RemoveByPatternAsync("scoped-provider-summaries:*");

            return new NoContentResult();
        }

        public async Task<IActionResult> ImportProviders(HttpRequest request)
        {
            MasterProviderModel[] providers = new MasterProviderModel[0];

            try
            {
                providers = await request.ReadBodyJson<MasterProviderModel[]>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Invalid providers were provided");

                return new BadRequestObjectResult($"Invalid providers were provided: {ex.Message}");
            }

            if (providers.IsNullOrEmpty())
            {
                _logger.Error("No providers were provided");

                return new BadRequestObjectResult("No providers were provided");
            }

            IEnumerable<ValidationFailure> validationFailures = providers.SelectMany(p => _masterProviderModelValidator.Validate(p).Errors);

            if (validationFailures.Any())
            {
                return new BadRequestObjectResult(string.Join(",", validationFailures.Select(vf => vf.ErrorMessage)));
            }

            IList<ProviderIndex> providersToIndex = new List<ProviderIndex>();

            foreach (MasterProviderModel provider in providers)
            {
                ProviderIndex providerIndex = _providerImportMappingService.Map(provider);

                if (providerIndex != null)
                {
                    providersToIndex.Add(providerIndex);
                }
            }

            IEnumerable<IndexError> errors = await _resultsSearchRepositoryPolicy.ExecuteAsync(() => _searchRepository.Index(providersToIndex));

            if (errors.Any())
            {
                string errorMessage = $"Failed to index providers result documents with errors: { string.Join(";", errors.Select(m => m.ErrorMessage)) }";
                _logger.Error(errorMessage);

                return new InternalServerErrorResult(errorMessage);
            }

            return new NoContentResult();
        }

        public async Task<IActionResult> HasCalculationResults(string calculationId)
        {
            Guard.IsNullOrWhiteSpace(calculationId, nameof(calculationId));

            Models.Calcs.Calculation calculation = await _calculationsRepositoryPolicy.ExecuteAsync(() => _calculationRepository.GetCalculationById(calculationId));

            if (calculation == null)
            {
                _logger.Error($"Calculation could not be found for calculation id '{calculationId}'");

                return new NotFoundObjectResult($"Calculation could not be found for calculation id '{calculationId}'");
            }

            ProviderResult providerResult = await _resultsRepositoryPolicy.ExecuteAsync(() => _resultsRepository.GetSingleProviderResultBySpecificationId(calculation.SpecificationId));

            if (providerResult != null)
            {
                CalculationResult calculationResult = providerResult.CalculationResults?.FirstOrDefault(m => string.Equals(m.Calculation.Id, calculationId, StringComparison.InvariantCultureIgnoreCase));

                if (calculationResult != null)
                {
                    return new OkObjectResult(true);
                }
            }

            return new OkObjectResult(false);
        }

        public async Task QueueCsvGenerationMessages()
        {
            IEnumerable<SpecificationSummary> specificationSummaries = await _specificationsRepositoryPolicy.ExecuteAsync(() => _specificationsRepository.GetSpecificationSummaries());

            if (specificationSummaries.IsNullOrEmpty())
            {
                string errorMessage = "No specification summaries found to generate calculation results csv.";

                _logger.Error(errorMessage);

                throw new RetriableException(errorMessage);
            }

            foreach (SpecificationSummary specificationSummary in specificationSummaries)
            {
                await QueueCsvGenerationMessage(specificationSummary.Id);
            }
        }

        public async Task QueueCsvGenerationMessage(string specificationId)
        {
            bool hasNewResults = await _resultsRepositoryPolicy.ExecuteAsync(
                () => _resultsRepository.CheckHasNewResultsForSpecificationIdAndTimePeriod(specificationId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)));

            if (hasNewResults)
            {
                _logger.Information($"Found new calculation results for specification id '{specificationId}'");

                await _messengerService.SendToQueue(ServiceBusConstants.QueueNames.CalculationResultsCsvGeneration,
                    string.Empty,
                    new Dictionary<string, string>
                    {
                        { "specification-id", specificationId }
                    });
            }
        }

        public async Task GenerateCalculationResultsCsv(Message message)
        {
            string specificationId = message.GetUserProperty<string>("specification-id");

            if (specificationId == null)
            {
                string error = "Specification id missing";

                _logger.Error(error);
                throw new NonRetriableException(error);
            }

            IEnumerable<ProviderResult> results = await ProviderResultsBySpecificationId(specificationId);

            IEnumerable<ExpandoObject> resultsForOutput = ProcessProviderResultsForCsvOutput(results).ToList();

            string csv = new CsvUtils().CreateCsvExpando(resultsForOutput);

            ICloudBlob blob = _blobClient.GetBlockBlobReference($"calculation-results-{specificationId}.csv");

            using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
            {
                await _blobClientPolicy.ExecuteAsync(() => blob.UploadFromStreamAsync(memoryStream));
            }
        }

        public IEnumerable<ExpandoObject> ProcessProviderResultsForCsvOutput(IEnumerable<ProviderResult> results)
        {
            var calculationNames = results
                .SelectMany(x => x.CalculationResults)
                .Select(x => x.Calculation.Name)
                .ToArray();

            foreach (ProviderResult result in results)
            {
                dynamic csvRow = new ExpandoObject();
                IDictionary<string, object> csvDict = csvRow as IDictionary<string, object>;

                csvDict["UKPRN"] = result.Provider.UKPRN;
                csvDict["ProviderName"] = result.Provider.Name;

                foreach (string calculationName in calculationNames)
                {
                    csvDict[calculationName] = result.CalculationResults
                        .Where(x => x.Calculation.Name == calculationName)
                        .Select(x => x.Value.ToString())
                        .FirstOrDefault();
                }

                yield return csvRow;
            }
        }
    }
}