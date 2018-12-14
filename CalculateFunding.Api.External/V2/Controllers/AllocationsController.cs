﻿using System.Threading.Tasks;
using CalculateFunding.Api.External.Swagger.OperationFilters;
using CalculateFunding.Api.External.V2.Interfaces;
using CalculateFunding.Api.External.V2.Models;
using CalculateFunding.Api.External.V2.Models.Examples;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Examples;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CalculateFunding.Api.External.V2.Controllers
{
    [Authorize(Roles = Constants.ExecuteApiRole)]
    [ApiVersion("2.0")]
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
        /// <param name="allocationVersion">An optional version reference for a specific version</param>
        [HttpGet("{allocationId}/{allocationVersion?}")]
        [Produces(typeof(AllocationModel))]
        [SwaggerResponseExample(200, typeof(AllocationExamples))]
        [SwaggerOperation("getAllocationById")]
        [SwaggerOperationFilter(typeof(OperationFilter<AllocationModel>))]
        [ProducesResponseType(typeof(AllocationModel), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public Task<IActionResult> GetAllocation(string allocationId, int? allocationVersion = null)
        {
            return _allocationsService.GetAllocationByAllocationResultId(allocationId, allocationVersion, Request);
        }

        /// <summary>
        /// Return a given allocation with its history. By default the latest published allocation is returned, or 404 if none is published.
        /// </summary>
        /// <param name="allocationId">The id of the requested allocation</param>
        [HttpGet("{allocationId}/history")]
        [Produces(typeof(AllocationWithHistoryModel))]
        [SwaggerResponseExample(200, typeof(AllocationWithHistoryExamples))]
        [SwaggerOperation("getAllocationAndHistoryById")]
        [SwaggerOperationFilter(typeof(OperationFilter<AllocationWithHistoryModel>))]
        [ProducesResponseType(typeof(AllocationModel), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public Task<IActionResult> GetAllocationAndHistory(string allocationId)
        {
            return _allocationsService.GetAllocationAndHistoryByAllocationResultId(allocationId, Request);
        }
    }
}