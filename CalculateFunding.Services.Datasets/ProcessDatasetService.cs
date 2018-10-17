﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalculateFunding.Models;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Health;
using CalculateFunding.Models.Results;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.Core.Interfaces.Caching;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Interfaces.ServiceBus;
using CalculateFunding.Services.Core.Interfaces.Services;
using CalculateFunding.Services.DataImporter;
using CalculateFunding.Services.Datasets.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Polly;
using Serilog;

namespace CalculateFunding.Services.Datasets
{
    public class ProcessDatasetService : IProcessDatasetService, IHealthChecker
    {
        private readonly IDatasetRepository _datasetRepository;
        private readonly IMessengerService _messengerService;
        private readonly IExcelDatasetReader _excelDatasetReader;
        private readonly ICacheProvider _cacheProvider;
        private readonly ICalcsRepository _calcsRepository;
        private readonly IBlobClient _blobClient;
        private readonly IProvidersResultsRepository _providersResultsRepository;
        private readonly IProviderRepository _providerRepository;
        private readonly IVersionRepository<ProviderSourceDatasetVersion> _sourceDatasetsVersionRepository;
        private readonly ILogger _logger;
        private readonly ITelemetry _telemetry;
        private readonly Policy _providerResultsRepositoryPolicy;

        public ProcessDatasetService(
            IDatasetRepository datasetRepository,
            IMessengerService messengerService,
            IExcelDatasetReader excelDatasetReader,
            ICacheProvider cacheProvider,
            ICalcsRepository calcsRepository,
            IBlobClient blobClient,
            IProvidersResultsRepository providersResultsRepository,
            IProviderRepository providerRepository,
            IVersionRepository<ProviderSourceDatasetVersion> sourceDatasetsVersionRepository,
            ILogger logger,
            ITelemetry telemetry,
            IDatasetsResiliencePolicies datasetsResiliencePolicies)
        {
            Guard.ArgumentNotNull(datasetRepository, nameof(datasetRepository));
            Guard.ArgumentNotNull(messengerService, nameof(messengerService));
            Guard.ArgumentNotNull(excelDatasetReader, nameof(excelDatasetReader));
            Guard.ArgumentNotNull(cacheProvider, nameof(cacheProvider));
            Guard.ArgumentNotNull(calcsRepository, nameof(calcsRepository));
            Guard.ArgumentNotNull(providersResultsRepository, nameof(providersResultsRepository));
            Guard.ArgumentNotNull(providerRepository, nameof(providerRepository));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(telemetry, nameof(telemetry));

            _datasetRepository = datasetRepository;
            _messengerService = messengerService;
            _excelDatasetReader = excelDatasetReader;
            _cacheProvider = cacheProvider;
            _calcsRepository = calcsRepository;
            _blobClient = blobClient;
            _providersResultsRepository = providersResultsRepository;
            _providerRepository = providerRepository;
            _sourceDatasetsVersionRepository = sourceDatasetsVersionRepository;
            _logger = logger;
            _telemetry = telemetry;

            _providerResultsRepositoryPolicy = datasetsResiliencePolicies.ProviderResultsRepository;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            var blobHealth = await _blobClient.IsHealthOk();
            ServiceHealth datasetsRepoHealth = await ((IHealthChecker)_datasetRepository).IsHealthOk();
            string queueName = ServiceBusConstants.QueueNames.CalculationJobInitialiser;
            var messengerServiceHealth = await _messengerService.IsHealthOk(queueName);
            var cacheHealth = await _cacheProvider.IsHealthOk();
            ServiceHealth providersResultsRepoHealth = await ((IHealthChecker)_providersResultsRepository).IsHealthOk();
            ServiceHealth providerRepoHealth = await ((IHealthChecker)_providerRepository).IsHealthOk();

            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(DatasetService)
            };
            health.Dependencies.Add(new DependencyHealth { HealthOk = blobHealth.Ok, DependencyName = _blobClient.GetType().GetFriendlyName(), Message = blobHealth.Message });
            health.Dependencies.AddRange(datasetsRepoHealth.Dependencies);
            health.Dependencies.Add(new DependencyHealth { HealthOk = messengerServiceHealth.Ok, DependencyName = $"{_messengerService.GetType().GetFriendlyName()} for queue: {queueName}", Message = messengerServiceHealth.Message });
            health.Dependencies.Add(new DependencyHealth { HealthOk = cacheHealth.Ok, DependencyName = _cacheProvider.GetType().GetFriendlyName(), Message = cacheHealth.Message });
            health.Dependencies.AddRange(providersResultsRepoHealth.Dependencies);
            health.Dependencies.AddRange(providerRepoHealth.Dependencies);

            return health;
        }

        async public Task<IActionResult> ProcessDataset(HttpRequest request)
        {
            string json = await request.GetRawBodyStringAsync();

            Dataset dataset = JsonConvert.DeserializeObject<Dataset>(json);

            request.Query.TryGetValue("specificationId", out var specId);

            string specificationId = specId.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(specificationId))
            {
                _logger.Error($"No {nameof(specificationId)}");

                return new BadRequestObjectResult($"Null or empty {nameof(specificationId)} provided");
            }

            request.Query.TryGetValue("relationshipId", out var relId);

            var relationshipId = relId.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                _logger.Error($"No {nameof(relationshipId)}");

                return new BadRequestObjectResult($"Null or empty {nameof(relationshipId)} provided");
            }

            DefinitionSpecificationRelationship relationship = await _datasetRepository.GetDefinitionSpecificationRelationshipById(relationshipId);

            if (relationship == null)
            {
                _logger.Error($"Relationship not found for relationship id: {relationshipId}");
                throw new ArgumentNullException(nameof(relationshipId), "A null or empty relationship returned from repository");
            }

            BuildProject buildProject = null;

            Reference user = request.GetUser();

            try
            {
                buildProject = await ProcessDataset(dataset, specificationId, relationshipId, relationship.DatasetVersion.Version, user);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, $"Failed to process data with exception: {exception.Message}");
            }

            if (buildProject != null && !buildProject.DatasetRelationships.IsNullOrEmpty() && buildProject.DatasetRelationships.Any(m => m.DefinesScope))
            {
                Message message = new Message();

                IDictionary<string, string> messageProperties = message.BuildMessageProperties();
                messageProperties.Add("specification-id", specificationId);
                messageProperties.Add("provider-cache-key", $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}");

                await _messengerService.SendToQueue<string>(ServiceBusConstants.QueueNames.CalculationJobInitialiser,
                        null, messageProperties);

                _telemetry.TrackEvent("InstructCalculationAllocationEventRun",
                      new Dictionary<string, string>()
                      {
                            { "specificationId" , buildProject.SpecificationId },
                            { "buildProjectId" , buildProject.Id },
                            { "datasetId", dataset.Id }
                      },
                      new Dictionary<string, double>()
                      {
                            { "InstructCalculationAllocationEventRunDataset" , 1 },
                            { "InstructCalculationAllocationEventRun" , 1 }
                      }
                );
            }

            return new OkResult();
        }

        async public Task ProcessDataset(Message message)
        {
            Guard.ArgumentNotNull(message, nameof(message));

            IDictionary<string, object> properties = message.UserProperties;

            Dataset dataset = message.GetPayloadAsInstanceOf<Dataset>();

            if (dataset == null)
            {
                _logger.Error("A null dataset was provided to ProcessData");

                throw new ArgumentNullException(nameof(dataset), "A null dataset was provided to ProcessDataset");
            }

            if (!message.UserProperties.ContainsKey("specification-id"))
            {
                _logger.Error("Specification Id key is missing in ProcessDataset message properties");
                throw new KeyNotFoundException("Specification Id key is missing in ProcessDataset message properties");
            }

            string specificationId = message.UserProperties["specification-id"].ToString();

            if (string.IsNullOrWhiteSpace(specificationId))
            {
                _logger.Error("A null or empty specification id was provided to ProcessData");

                throw new ArgumentNullException(nameof(specificationId), "A null or empty specification id was provided to ProcessData");
            }

            if (!message.UserProperties.ContainsKey("relationship-id"))
            {
                _logger.Error("Relationship Id key is missing in ProcessDataset message properties");
                throw new KeyNotFoundException("Relationship Id key is missing in ProcessDataset message properties");
            }

            string relationshipId = message.UserProperties["relationship-id"].ToString();
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                _logger.Error("A null or empty relationship id was provided to ProcessDataset");

                throw new ArgumentNullException(nameof(specificationId), "A null or empty relationship id was provided to ProcessData");
            }

            DefinitionSpecificationRelationship relationship = await _datasetRepository.GetDefinitionSpecificationRelationshipById(relationshipId);

            if (relationship == null)
            {
                _logger.Error($"Relationship not found for relationship id: {relationshipId}");
                throw new ArgumentNullException(nameof(relationshipId), "A null or empty relationship returned from repository");
            }

            BuildProject buildProject = null;

            Reference user = message.GetUserDetails();

            try
            {
                buildProject = await ProcessDataset(dataset, specificationId, relationshipId, relationship.DatasetVersion.Version, user);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, $"Failed to run ProcessDataset with exception: {exception.Message} for relationship ID '{relationshipId}'");
                throw;
            }

            if (buildProject != null && !buildProject.DatasetRelationships.IsNullOrEmpty() && buildProject.DatasetRelationships.Any(m => m.DefinesScope))
            {
                IDictionary<string, string> messageProperties = message.BuildMessageProperties();
                messageProperties.Add("specification-id", specificationId);
                messageProperties.Add("provider-cache-key", $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}");

                await _messengerService.SendToQueue<string>(ServiceBusConstants.QueueNames.CalculationJobInitialiser,
                        null, messageProperties);

                _telemetry.TrackEvent("InstructCalculationAllocationEventRun",
                      new Dictionary<string, string>()
                      {
                            { "specificationId" , buildProject.SpecificationId },
                            { "buildProjectId" , buildProject.Id },
                            { "datasetId", dataset.Id }
                      },
                      new Dictionary<string, double>()
                      {
                            { "InstructCalculationAllocationEventRunDataset" , 1 },
                            { "InstructCalculationAllocationEventRun" , 1 }
                      }
                );
            }
        }

        // This work is still underway, checking in current progress and will resume later
        public Task<DatasetAggregations> GenerateAggregations(DatasetDefinition datasetDefinition, TableLoadResult tableLoadResult)
        {
            return null;
        }

        async Task<BuildProject> ProcessDataset(Dataset dataset, string specificationId, string relationshipId, int version, Reference user)
        {
            string dataDefinitionId = dataset.Definition.Id;

            DatasetVersion datasetVersion = dataset.History.Where(v => v.Version == version).SingleOrDefault();
            if (datasetVersion == null)
            {
                _logger.Error("Dataset version not found for dataset '{name}' ({id}) version '{version}'", dataset.Id, dataset.Name, version);
                throw new InvalidOperationException($"Dataset version not found for dataset '{dataset.Name}' ({dataset.Name}) version '{version}'");
            }

            string fullBlobName = datasetVersion.BlobName;

            DatasetDefinition datasetDefinition =
                    (await _datasetRepository.GetDatasetDefinitionsByQuery(m => m.Id == dataDefinitionId))?.FirstOrDefault();

            if (datasetDefinition == null)
            {
                _logger.Error($"Unable to find a data definition for id: {dataDefinitionId}, for blob: {fullBlobName}");

                throw new Exception($"Unable to find a data definition for id: {dataDefinitionId}, for blob: {fullBlobName}");
            }

            BuildProject buildProject = await _calcsRepository.GetBuildProjectBySpecificationId(specificationId);

            if (buildProject == null)
            {
                _logger.Error($"Unable to find a build project for specification id: {specificationId}");

                throw new Exception($"Unable to find a build project for id: {specificationId}");
            }

            TableLoadResult loadResult = await GetTableResult(fullBlobName, datasetDefinition);

            if (loadResult == null)
            {
                _logger.Error($"Failed to load table result");

                throw new Exception($"Failed to load table result");
            }

            await PersistDataset(loadResult, dataset, datasetDefinition, buildProject, specificationId, relationshipId, version, user);

            return buildProject;
        }

        async Task<TableLoadResult> GetTableResult(string fullBlobName, DatasetDefinition datasetDefinition)
        {

            string dataset_cache_key = $"{CacheKeys.DatasetRows}:{datasetDefinition.Id}:{GetBlobNameCacheKey(fullBlobName)}".ToLowerInvariant();

            IEnumerable<TableLoadResult> tableLoadResults = await _cacheProvider.GetAsync<TableLoadResult[]>(dataset_cache_key);

            if (tableLoadResults.IsNullOrEmpty())
            {
                ICloudBlob blob = await _blobClient.GetBlobReferenceFromServerAsync(fullBlobName);

                if (blob == null)
                {
                    _logger.Error($"Failed to find blob with path: {fullBlobName}");
                    throw new ArgumentException($"Failed to find blob with path: {fullBlobName}");
                }

                using (Stream datasetStream = await _blobClient.DownloadToStreamAsync(blob))
                {
                    if (datasetStream == null || datasetStream.Length == 0)
                    {
                        _logger.Error($"Invalid blob returned: {fullBlobName}");
                        throw new ArgumentException($"Invalid blob returned: {fullBlobName}");
                    }

                    tableLoadResults = _excelDatasetReader.Read(datasetStream, datasetDefinition).ToList();
                }

                await _cacheProvider.SetAsync(dataset_cache_key, tableLoadResults.ToArraySafe(), TimeSpan.FromDays(7), true);
            }

            return tableLoadResults.FirstOrDefault();
        }

        async Task PersistDataset(TableLoadResult loadResult, Dataset dataset, DatasetDefinition datasetDefinition, BuildProject buildProject, string specificationId, string relationshipId, int version, Reference user)
        {

            IEnumerable<ProviderSummary> providerSummaries = await _providerRepository.GetAllProviderSummaries();


            Guard.IsNullOrWhiteSpace(relationshipId, nameof(relationshipId));

            IList<ProviderSourceDataset> providerSourceDatasets = new List<ProviderSourceDataset>();

            if (buildProject.DatasetRelationships == null)
            {
                _logger.Error($"No dataset relationships found for build project with id : '{buildProject.Id}' for specification '{specificationId}'");
                return;
            }

            DatasetRelationshipSummary relationshipSummary = buildProject.DatasetRelationships.FirstOrDefault(m => m.Relationship.Id == relationshipId);

            if (relationshipSummary == null)
            {
                _logger.Error($"No dataset relationship found for build project with id : {buildProject.Id} with data definition id {datasetDefinition.Id} and relationshipId '{relationshipId}'");
                return;
            }

            Dictionary<string, ProviderSourceDataset> existingCurrent = new Dictionary<string, ProviderSourceDataset>();

            IEnumerable<ProviderSourceDataset> existingCurrentDatasets = await _providerResultsRepositoryPolicy.ExecuteAsync(() =>
                _providersResultsRepository.GetCurrentProviderSourceDatasets(specificationId, relationshipId));

            if (existingCurrentDatasets.AnyWithNullCheck())
            {
                foreach (ProviderSourceDataset currentDataset in existingCurrentDatasets)
                {
                    existingCurrent.Add(currentDataset.ProviderId, currentDataset);
                }
            }

            ConcurrentDictionary<string, ProviderSourceDataset> resultsByProviderId = new ConcurrentDictionary<string, ProviderSourceDataset>();

            ConcurrentDictionary<string, ProviderSourceDataset> updateCurrentDatasets = new ConcurrentDictionary<string, ProviderSourceDataset>();

            Parallel.ForEach(loadResult.Rows, (RowLoadResult row) =>
            {
                IEnumerable<string> allProviderIds = GetProviderIdsForIdentifier(datasetDefinition, row, providerSummaries);

                foreach (string providerId in allProviderIds)
                {
                    if (!resultsByProviderId.TryGetValue(providerId, out ProviderSourceDataset sourceDataset))
                    {
                        sourceDataset = new ProviderSourceDataset
                        {
                            DataGranularity = relationshipSummary.DataGranularity,
                            SpecificationId = specificationId,
                            DefinesScope = relationshipSummary.DefinesScope,
                            DataDefinition = new Reference(relationshipSummary.DatasetDefinition.Id, relationshipSummary.DatasetDefinition.Name),
                            DataRelationship = new Reference(relationshipSummary.Relationship.Id, relationshipSummary.Relationship.Name),
                            DatasetRelationshipSummary = new Reference(relationshipSummary.Id, relationshipSummary.Name),
                            ProviderId = providerId,
                        };

                        sourceDataset.Current = new ProviderSourceDatasetVersion
                        {
                            Rows = new List<Dictionary<string, object>>(),
                            Dataset = new VersionReference(dataset.Id, dataset.Name, version),
                            Date = DateTimeOffset.Now.ToLocalTime(),
                            ProviderId = providerId,
                            Version = 1,
                            PublishStatus = PublishStatus.Draft,
                            ProviderSourceDatasetId = sourceDataset.Id,
                            Author = user
                        };

                        if (!resultsByProviderId.TryAdd(providerId, sourceDataset))
                        {
                            resultsByProviderId.TryGetValue(providerId, out sourceDataset);
                        }
                    }
                    sourceDataset.Current.Rows.Add(row.Fields);
                }
            });

            ConcurrentBag<ProviderSourceDatasetVersion> historyToSave = new ConcurrentBag<ProviderSourceDatasetVersion>();

            List<Task> historySaveTasks = new List<Task>(resultsByProviderId.Count);

            SemaphoreSlim throttler = new SemaphoreSlim(initialCount: 15);
            foreach (KeyValuePair<string, ProviderSourceDataset> providerSourceDataset in resultsByProviderId)
            {
                await throttler.WaitAsync();
                historySaveTasks.Add(
                    Task.Run(async () =>
                    {
                        try
                        {
                            string providerId = providerSourceDataset.Key;
                            ProviderSourceDataset sourceDataset = providerSourceDataset.Value;

                            ProviderSourceDatasetVersion newVersion = null;

                            if (existingCurrent.ContainsKey(providerId))
                            {
                                newVersion = existingCurrent[providerId].Current.Clone() as ProviderSourceDatasetVersion;

                                string existingDatasetJson = JsonConvert.SerializeObject(existingCurrent[providerId].Current.Rows);
                                string latestDatasetJson = JsonConvert.SerializeObject(sourceDataset.Current.Rows);

                                if (existingDatasetJson != latestDatasetJson)
                                {
                                    newVersion = await _sourceDatasetsVersionRepository.CreateVersion(newVersion, existingCurrent[providerId].Current, providerId);
                                    newVersion.Author = user;
                                    newVersion.Rows = sourceDataset.Current.Rows;

                                    sourceDataset.Current = newVersion;

                                    updateCurrentDatasets.TryAdd(providerId, sourceDataset);

                                    historyToSave.Add(newVersion);
                                }
                            }
                            else
                            {
                                newVersion = sourceDataset.Current;

                                updateCurrentDatasets.TryAdd(providerId, sourceDataset);

                                historyToSave.Add(newVersion);
                            }
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }));
            }

            await TaskHelper.WhenAllAndThrow(historySaveTasks.ToArray());

            if (updateCurrentDatasets.Count > 0)
            {
                _logger.Information($"Saving {updateCurrentDatasets.Count()} updated source datasets");

                await _providerResultsRepositoryPolicy.ExecuteAsync(() =>
                _providersResultsRepository.UpdateCurrentProviderSourceDatasets(updateCurrentDatasets.Values));
            }

            if (historyToSave.Any())
            {
                _logger.Information($"Saving {historyToSave.Count()} items to history");
                await _sourceDatasetsVersionRepository.SaveVersions(historyToSave);
            }

            await PopulateProviderSummariesForSpecification(specificationId, providerSummaries);
        }

        private static IEnumerable<string> GetProviderIdsForIdentifier(DatasetDefinition datasetDefinition, RowLoadResult row, IEnumerable<ProviderSummary> providerSummaries)
        {
            IEnumerable<FieldDefinition> identifierFields = datasetDefinition.TableDefinitions?.First().FieldDefinitions.Where(x => x.IdentifierFieldType.HasValue);

            foreach (FieldDefinition field in identifierFields)
            {
                if (!string.IsNullOrWhiteSpace(field.Name))
                {
                    if (row.Fields.ContainsKey(field.Name))
                    {
                        string identifier = row.Fields[field.Name]?.ToString();
                        if (!string.IsNullOrWhiteSpace(identifier))
                        {
                            Dictionary<string, List<string>> lookup = GetDictionaryForIdentifierType(field.IdentifierFieldType, identifier, providerSummaries);
                            if (lookup.TryGetValue(identifier, out List<string> providerIds))
                            {
                                return providerIds;
                            }
                        }
                        else
                        {
                            // For debugging only
                            //_logger.Debug("Found identifier with null or emtpy string for provider");
                        }
                    }
                }
            }

            return new string[0];
        }

        async Task PopulateProviderSummariesForSpecification(string specificationId, IEnumerable<ProviderSummary> allCachedProviders)
        {
            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            IEnumerable<string> providerIdsAll = await _providerResultsRepositoryPolicy.ExecuteAsync(() =>
                _providersResultsRepository.GetAllProviderIdsForSpecificationid(specificationId));

            IList<ProviderSummary> providerSummaries = new List<ProviderSummary>();

            foreach (string providerId in providerIdsAll)
            {
                ProviderSummary cachedProvider = allCachedProviders.FirstOrDefault(m => m.Id == providerId);

                if (cachedProvider != null)
                {
                    providerSummaries.Add(cachedProvider);
                }
            }

            await _cacheProvider.KeyDeleteAsync<ProviderSummary>(cacheKey);
            await _cacheProvider.CreateListAsync<ProviderSummary>(providerSummaries, cacheKey);
        }

        /// <summary>
        /// Gets list of Provider IDs from the given Identifier Type and Identifier Value
        /// </summary>
        /// <param name="identifierFieldType">Identifier Type</param>
        /// <param name="fieldIdentifierValue">Identifier ID - eg UPIN value</param>
        /// <returns>List of Provider IDs matching the given identifiers</returns>
        private static Dictionary<string, List<string>> GetDictionaryForIdentifierType(IdentifierFieldType? identifierFieldType, string fieldIdentifierValue, IEnumerable<ProviderSummary> providerSummaries)
        {
            if (!identifierFieldType.HasValue)
            {
                return new Dictionary<string, List<string>>();
            }

            // Expression to filter ProviderSummaries - this selects which field on the ProviderSummary to filter on, eg UPIN
            Func<ProviderSummary, string> identifierSelectorExpression = GetIdentifierSelectorExpression(identifierFieldType.Value);

            // Find ProviderIds from the list of all providers - given the field and value of the ID
            IEnumerable<string> filteredIdentifiers = providerSummaries.Where(x => identifierSelectorExpression(x) == fieldIdentifierValue).Select(m => m.Id);

            return new Dictionary<string, List<string>> { { fieldIdentifierValue, filteredIdentifiers.ToList() } };
        }

        private static Func<ProviderSummary, string> GetIdentifierSelectorExpression(IdentifierFieldType identifierFieldType)
        {
            if (identifierFieldType == IdentifierFieldType.URN)
            {
                return x => x.URN;
            }
            else if (identifierFieldType == IdentifierFieldType.Authority)
            {
                return x => x.Authority;
            }
            else if (identifierFieldType == IdentifierFieldType.EstablishmentNumber)
            {
                return x => x.EstablishmentNumber;
            }
            else if (identifierFieldType == IdentifierFieldType.UKPRN)
            {
                return x => x.UKPRN;
            }
            else if (identifierFieldType == IdentifierFieldType.UPIN)
            {
                return x => x.UPIN;
            }
            else
            {
                return null;
            }
        }

        public static string GetBlobNameCacheKey(string blobPath)
        {
            byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(blobPath.ToLowerInvariant());
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}