﻿using CalculateFunding.Api.External.Swagger.OperationFilters;
using CalculateFunding.Api.External.V1.Interfaces;
using CalculateFunding.Api.External.V1.Models;
using CalculateFunding.Api.External.V1.Models.Examples;
using CalculateFunding.Models.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Threading.Tasks;

namespace CalculateFunding.Api.External.V1.Controllers
{
    [Authorize(Roles = Constants.ExecuteApiRole)]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/allocations")]
    public class AllocationsController : Controller
    {
        private readonly IAllocationsService _allocationsService;

        public AllocationsController(IAllocationsService allocationsService)
        {
            _allocationsService = allocationsService;
        }

        /// <summary>
        /// Return a given allocation. By default the latest published allocation is returned, or 404 if none is published. 
        /// An optional specific version can be requested
        /// </summary>
        /// <param name="allocationId">The id of the requested allocation</param>
        [HttpGet("{allocationId}")]
        [Produces(typeof(AllocationModel))]
        [SwaggerResponseExample(200, typeof(AllocationExamples))]
        [SwaggerOperation("getAllocationById")]
        [SwaggerOperationFilter(typeof(OperationFilter<AllocationModel>))]
        [ProducesResponseType(typeof(AllocationModel), 200)]
        [ProducesResponseType(304)]
        [ProducesResponseType(401)]
        [ProducesResponseType(406)]
        [ProducesResponseType(500)]
        [SwaggerResponseHeader(200, "ETag", "string", "An ETag of the resource")]
        [SwaggerResponseHeader(200, "Cache-Control", "string", "Caching information for the resource")]
        [SwaggerResponseHeader(200, "Last-Modified", "date", "Date the resource was last modified")]
        public IActionResult GetAllocation(string allocationId)
        {
            return _allocationsService.GetAllocationByAllocationResultId(allocationId, Request);
        }
    }
}
