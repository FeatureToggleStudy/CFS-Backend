﻿using CalculateFunding.Models.Health;
using CalculateFunding.Models.Results;
using CalculateFunding.Repositories.Common.Cosmos;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Interfaces.Services;
using CalculateFunding.Services.Results.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Results
{
    public class ProviderSourceDatasetRepository : IProviderSourceDatasetRepository, IHealthChecker
    {
        private readonly CosmosRepository _cosmosRepository;

        public ProviderSourceDatasetRepository(CosmosRepository cosmosRepository)
        {
            _cosmosRepository = cosmosRepository;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            var cosmosRepoHealth = await _cosmosRepository.IsHealthOk();

            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProviderSourceDatasetRepository)
            };
            health.Dependencies.Add(new DependencyHealth { HealthOk = cosmosRepoHealth.Ok, DependencyName = _cosmosRepository.GetType().GetFriendlyName(), Message = cosmosRepoHealth.Message });

            return health;
        }

        public Task<HttpStatusCode> UpsertProviderSourceDataset(ProviderSourceDatasetCurrent providerSourceDataset)
        {
            return _cosmosRepository.CreateAsync(providerSourceDataset);
        }

        public Task<IEnumerable<ProviderSourceDatasetCurrent>> GetProviderSourceDatasets(string providerId, string specificationId)
        {
            return _cosmosRepository.QueryPartitionedEntity<ProviderSourceDatasetCurrent>($"SELECT * FROM Root r WHERE r.content.providerId = '{providerId}' AND r.content.specificationId = '{specificationId}' AND r.documentType = '{nameof(ProviderSourceDatasetCurrent)}' AND r.deleted = false", -1, specificationId);
        }

        public async Task<IEnumerable<string>> GetAllScopedProviderIdsForSpecificationId(string specificationId)
        {
            IEnumerable< ProviderSourceDatasetCurrent> providerSourceDatasets = await _cosmosRepository.QueryPartitionedEntity<ProviderSourceDatasetCurrent>($"SELECT * FROM Root r WHERE r.content.specificationId = '{specificationId}' AND r.documentType = '{nameof(ProviderSourceDatasetCurrent)}' AND r.deleted = false", -1, specificationId);

            return providerSourceDatasets.Select(m => m.ProviderId).Distinct();
        }
    }
}
