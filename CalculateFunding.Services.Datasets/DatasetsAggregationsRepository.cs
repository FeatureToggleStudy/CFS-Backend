﻿using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Health;
using CalculateFunding.Repositories.Common.Cosmos;
using CalculateFunding.Services.Core.Interfaces.Services;
using CalculateFunding.Services.Datasets.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Datasets
{
    public class DatasetsAggregationsRepository : IDatasetsAggregationsRepository, IHealthChecker
    {
        private readonly CosmosRepository _cosmosRepository;

        public DatasetsAggregationsRepository(CosmosRepository cosmosRepository)
        {
            _cosmosRepository = cosmosRepository;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            ServiceHealth health = new ServiceHealth();

            var cosmosHealth = await _cosmosRepository.IsHealthOk();

            health.Name = nameof(DatasetsAggregationsRepository);
            health.Dependencies.Add(new DependencyHealth { HealthOk = cosmosHealth.Ok, DependencyName = this.GetType().Name, Message = cosmosHealth.Message });

            return health;
        }

        public async Task CreateDatasetAggregations(DatasetAggregations datasetAggregations)
        {
            await _cosmosRepository.CreateAsync<DatasetAggregations>(datasetAggregations);
        }

        public Task<IEnumerable<DatasetAggregations>> GetDatasetAggregationsForSpecificationId(string specificationId)
        {
            IEnumerable<DatasetAggregations> results = _cosmosRepository.Query<DatasetAggregations>().Where(x => x.SpecificationId == specificationId).ToList();

            return Task.FromResult(results);
        }
    }
}