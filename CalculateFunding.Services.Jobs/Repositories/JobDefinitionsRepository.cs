﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Jobs;
using CalculateFunding.Services.Jobs.Interfaces;

namespace CalculateFunding.Services.Jobs.Repositories
{
    public class JobDefinitionsRepository : IJobDefinitionsRepository, IHealthChecker
    {
        private readonly CosmosRepository _cosmosRepository;

        public JobDefinitionsRepository(CosmosRepository cosmosRepository)
        {
            _cosmosRepository = cosmosRepository;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            ServiceHealth health = new ServiceHealth();

            (bool Ok, string Message) cosmosHealth = await _cosmosRepository.IsHealthOk();

            health.Name = nameof(JobDefinitionsRepository);
            health.Dependencies.Add(new DependencyHealth { HealthOk = cosmosHealth.Ok, DependencyName = this.GetType().Name, Message = cosmosHealth.Message });

            return health;
        }

        public async Task<HttpStatusCode> SaveJobDefinition(JobDefinition definition)
        {
            return await _cosmosRepository.CreateAsync(definition);
        }

        public IEnumerable<JobDefinition> GetJobDefinitions()
        {
            IQueryable<JobDefinition> jobDefinitions = _cosmosRepository.Query<JobDefinition>();

            return jobDefinitions.AsEnumerable();
        }

        public async Task<JobDefinition> GetJobDefinitionById(string jobDefinitionId)
        {
            Guard.IsNullOrWhiteSpace(jobDefinitionId, nameof(jobDefinitionId));

            DocumentEntity<JobDefinition> jobDefinition = await _cosmosRepository.ReadAsync<JobDefinition>(jobDefinitionId);

            if (jobDefinition != null)
            {
                return jobDefinition.Content;
            }

            return null;
        }
    }
}
