﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;

namespace CalculateFunding.Services.Results.Interfaces
{
    public interface IResultsService
    {
	    Task<IActionResult> GetProviderResults(HttpRequest request);

        Task<IActionResult> GetProviderSpecifications(HttpRequest request);

        Task<IActionResult> GetProviderById(HttpRequest request);

        Task<IActionResult> GetProviderResultsBySpecificationId(HttpRequest request);

        Task<IActionResult> GetProviderSourceDatasetsByProviderIdAndSpecificationId(HttpRequest request);

        Task<IActionResult> ReIndexCalculationProviderResults();

        Task<IActionResult> GetScopedProviderIdsBySpecificationId(HttpRequest request);

        Task<IActionResult> GetFundingCalculationResultsForSpecifications(HttpRequest request);

        Task<IActionResult> ImportProviders(HttpRequest request);

        Task CleanupProviderResultsForSpecification(Message message);

        Task<IActionResult> RemoveCurrentProviders();

        Task<IActionResult> HasCalculationResults(string calculationId);

        Task QueueCsvGenerationMessages();

        Task QueueCsvGenerationMessage(string specificationId);

        Task GenerateCalculationResultsCsv(Message message);
    }
}
