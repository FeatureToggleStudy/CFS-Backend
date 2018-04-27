using System;
using System.Threading.Tasks;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Functions.LocalDebugProxy.Controllers
{
    public class ResultsController : BaseController
	{
		private readonly IResultsService _resultsService;
		private readonly IResultsSearchService _resultsSearchService;
        private readonly ICalculationProviderResultsSearchService _calculationProviderResultsSearchService;

        public ResultsController(
             IServiceProvider serviceProvider,
			 IResultsService resultsService, 
             IResultsSearchService resultsSearchService,
             ICalculationProviderResultsSearchService calculationProviderResultsSearchService)
			: base(serviceProvider)
		{
			Guard.ArgumentNotNull(resultsSearchService, nameof(resultsSearchService));

			_resultsService = resultsService;
			_resultsSearchService = resultsSearchService;
            _calculationProviderResultsSearchService = calculationProviderResultsSearchService;
        }

		[Route("api/results/providers-search")]
		[HttpPost]
		public Task<IActionResult> RunProvidersSearch()
		{
			SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

			return _resultsSearchService.SearchProviders(ControllerContext.HttpContext.Request);
		}

		[Route("api/results/get-provider-specs")]
		[HttpGet]
		public Task<IActionResult> RunGetProviderSpecifications()
		{
			SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

			return _resultsService.GetProviderSpecifications(ControllerContext.HttpContext.Request);
		}


		[Route("api/results/get-provider-results")]
		[HttpGet]
		public Task<IActionResult> RunGetProviderResults()
		{
			SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

			return _resultsService.GetProviderResults(ControllerContext.HttpContext.Request);
		}

        [Route("api/results/get-provider")]
        [HttpGet]
        public Task<IActionResult> RunGetProvider()
        {
            SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

            return _resultsService.GetProviderById(ControllerContext.HttpContext.Request);
        }

        [Route("api/results/update-provider-source-dataset")]
        [HttpPost]
        public Task<IActionResult> RunUpdateProviderSourceDataset()
        {
            SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

            return _resultsService.UpdateProviderSourceDataset(ControllerContext.HttpContext.Request);
        }

        [Route("api/results/get-provider-source-datasets")]
        [HttpGet]
        public Task<IActionResult> RunGetProviderSourceDatasetsByProviderIdAndSpecificationId()
        {
            SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

            return _resultsService.GetProviderSourceDatasetsByProviderIdAndSpecificationId(ControllerContext.HttpContext.Request);
        }

        [Route("api/results/reindex-calc-provider-results")]
        [HttpGet]
        public Task<IActionResult> RunReIndexCalculationProviderResults()
        {
            SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

            return _resultsService.ReIndexCalculationProviderResults();
        }

        [Route("api/results/calculation-provider-results-search")]
        [HttpPost]
        public Task<IActionResult> RunCalculationProviderResultsSearch()
        {
            SetUserAndCorrelationId(ControllerContext.HttpContext.Request);

            return _calculationProviderResultsSearchService.SearchCalculationProviderResults(ControllerContext.HttpContext.Request);
        }
    }
}