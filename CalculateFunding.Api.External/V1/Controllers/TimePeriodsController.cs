﻿using System.Collections.Generic;
using System.Threading.Tasks;
using CalculateFunding.Api.External.Swagger.OperationFilters;
using CalculateFunding.Api.External.V1.Interfaces;
using CalculateFunding.Api.External.V1.Models;
using CalculateFunding.Api.External.V1.Models.Examples;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CalculateFunding.Api.External.V1.Controllers
{
	[Authorize(Roles = Constants.ExecuteApiRole)]
	[Produces("application/vnd.sfa.allocation.1+json")]
    [Route("api/periods")]
    public class TimePeriodsController : Controller
    {
	    private readonly ITimePeriodsService _timePeriodsService;

	    public TimePeriodsController(ITimePeriodsService timePeriodsService)
	    {
		    _timePeriodsService = timePeriodsService;
	    }

	    /// <summary>
        /// Returns the time periods supported by the service
        /// </summary>
        /// <returns>A list of time periods </returns>
        [HttpGet]
        [Produces(typeof(List<Period>))]
        [SwaggerResponseExample(200, typeof(PeriodExamples))]
        [SwaggerOperation("getTimePeriods")]
        [SwaggerOperationFilter(typeof(OperationFilter<List<Period>>))]
        [ProducesResponseType(typeof(List<Period>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]

        public Task<IActionResult> Get()
	    {
		    return _timePeriodsService.GetFundingPeriods(Request);
	    }
    }
}
