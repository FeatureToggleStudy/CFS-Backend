﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Models.Aggregations;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Services.Calcs.Interfaces;
using CalculateFunding.Services.Core.Helpers;

namespace CalculateFunding.Services.Calcs
{
    public class CalculationsRepository : ICalculationsRepository, IHealthChecker
    {
        private readonly CosmosRepository _cosmosRepository;

        public CalculationsRepository(CosmosRepository cosmosRepository)
        {
            _cosmosRepository = cosmosRepository;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            ServiceHealth health = new ServiceHealth();

            var cosmosHealth = await _cosmosRepository.IsHealthOk();

            health.Name = nameof(CalculationsRepository);
            health.Dependencies.Add(new DependencyHealth { HealthOk = cosmosHealth.Ok, DependencyName = this.GetType().Name, Message = cosmosHealth.Message });

            return health;
        }

        public Task<HttpStatusCode> CreateDraftCalculation(Calculation calculation)
        {
            return _cosmosRepository.CreateAsync(calculation);
        }

        async public Task<Calculation> GetCalculationById(string calculationId)
        {
            var calculation = await _cosmosRepository.ReadAsync<Calculation>(calculationId);

            if (calculation == null)
            {
                return null;
            }

            return calculation.Content;
        }

        public Task<IEnumerable<Calculation>> GetCalculationsBySpecificationId(string specificationId)
        {
            IQueryable<Calculation> calculations = _cosmosRepository.Query<Calculation>().Where(x => x.SpecificationId == specificationId);

            return Task.FromResult(calculations.AsEnumerable());
        }

        public Task<Calculation> GetCalculationByCalculationSpecificationId(string calculationSpecificationId)
        {
            IQueryable<Calculation> calculations = _cosmosRepository.Query<Calculation>().Where(x => x.CalculationSpecification.Id == calculationSpecificationId);

            return Task.FromResult(calculations.AsEnumerable().FirstOrDefault());
        }

        public Task<HttpStatusCode> UpdateCalculation(Calculation calculation)
        {
            return _cosmosRepository.UpdateAsync(calculation);
        }

        public Task<IEnumerable<Calculation>> GetAllCalculations()
        {
            var calculations = _cosmosRepository.Query<Calculation>();

            return Task.FromResult(calculations.AsEnumerable());
        }

        public Task UpdateCalculations(IEnumerable<Calculation> calculations)
        {
            return _cosmosRepository.BulkUpsertAsync(calculations.ToList());
        }

        public async Task<StatusCounts> GetStatusCounts(string specificationId)
        {
            StatusCounts statusCounts = new StatusCounts();

            Task approvedCountTask = Task.Run(() =>
            {
                IQueryable<int> result = _cosmosRepository.RawQuery<int>($"SELECT VALUE COUNT(1) FROM c where c.documentType = 'Calculation' and c.content.current.publishStatus = 'Approved' and c.content.specificationId = '{specificationId}'", 1, true);

                statusCounts.Approved = result.AsEnumerable().First();
            });

            Task updatedCountTask = Task.Run(() =>
            {
                IQueryable<int> result = _cosmosRepository.RawQuery<int>($"SELECT VALUE COUNT(1) FROM c where c.documentType = 'Calculation' and c.content.current.publishStatus = 'Updated' and c.content.specificationId = '{specificationId}'", 1, true);

                statusCounts.Updated = result.AsEnumerable().First();
            });

            Task draftCountTask = Task.Run(() =>
            {
                IQueryable<int> result = _cosmosRepository.RawQuery<int>($"SELECT VALUE COUNT(1) FROM c where c.documentType = 'Calculation' and c.content.current.publishStatus = 'Draft'  and c.content.specificationId = '{specificationId}'", 1, true);

                statusCounts.Draft = result.AsEnumerable().First();
            });

            await TaskHelper.WhenAllAndThrow(approvedCountTask, updatedCountTask, draftCountTask);

            return statusCounts;
        }
    }
}
