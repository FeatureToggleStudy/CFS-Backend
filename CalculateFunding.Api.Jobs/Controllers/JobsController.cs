﻿using System.Collections.Generic;
using System.Threading.Tasks;
using CalculateFunding.Models;
using CalculateFunding.Models.Jobs;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Jobs.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Api.Jobs.Controllers
{
    public class JobsController : Controller
    {
        private readonly IJobService _jobService;
        private readonly IJobManagementService _jobManagementService;
        private readonly IJobDefinitionsService _jobDefinitionsService;

        public JobsController(IJobService jobService,
            IJobManagementService jobManagementService, IJobDefinitionsService jobDefinitionsService)
        {
            _jobService = jobService;
            _jobManagementService = jobManagementService;
            _jobDefinitionsService = jobDefinitionsService;
        }

        [HttpGet]
        [Route("api/jobs/{jobId}")]
        [ProducesResponseType(200, Type = typeof(JobCurrentModel))]
        public async Task<IActionResult> GetJobById(string jobId, bool includeChildJobs = false)
        {
            return await _jobService.GetJobById(jobId, includeChildJobs, ControllerContext.HttpContext.Request);
        }

        [HttpPut]
        [Route("api/jobs/{jobId}")]
        [ProducesResponseType(200, Type = typeof(JobSummary))]
        public async Task<IActionResult> UpdateJob(string jobId, JobUpdateModel jobUpdate)
        {
            return await _jobService.UpdateJob(jobId, jobUpdate, ControllerContext.HttpContext.Request);
        }

        [HttpPost]
        [Route("api/jobs")]
        [ProducesResponseType(201, Type = typeof(IEnumerable<JobSummary>))]
        public async Task<IActionResult> CreateJobs(IEnumerable<JobCreateModel> jobs)
        {
            return await _jobManagementService.CreateJobs(jobs, ControllerContext.HttpContext.Request);
        }

        [HttpGet]
        [Route("api/jobs/jobdefinitions/{jobDefinitionId}")]
        [ProducesResponseType(200, Type = typeof(JobDefinition))]
        public async Task<IActionResult> GetJobDefinitionById(string jobDefinitionId)
        {
            return await _jobDefinitionsService.GetJobDefinitionById(jobDefinitionId);
        }

        [HttpGet]
        [Route("api/jobs/jobdefinitions")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<JobDefinition>))]
        public async Task<IActionResult> GetJobDefinitions()
        {
            return await _jobDefinitionsService.GetJobDefinitions();
        }

        [HttpPost]
        [Route("api/jobs/jobdefinitions")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> SaveJobDefinition()
        {
            return await _jobDefinitionsService.SaveDefinition(ControllerContext.HttpContext.Request);
        }

        [HttpGet]
        [Route("api/jobs")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<JobSummary>))]
        public async Task<IActionResult> GetJobs(
                                                    [FromQuery] string specificationId = null,
                                                    [FromQuery] string jobType = null,
                                                    [FromQuery] string entityId = null,
                                                    [FromQuery] RunningStatus? runningStatus = null,
                                                    [FromQuery] CompletionStatus? completionStatus = null,
                                                    [FromQuery] bool excludeChildJobs = false,
                                                    [FromQuery] int pageNumber = 1)
        {
            // Sorted by last updated DESC
            // When excludeChildJobs == true, then return jobs with null ParentJobId

            // Any combination of above filters, when a filter is null, all jobs for that filter is displayed
            // Have fun with cross partition queries :)
            return await _jobService.GetJobs(specificationId, jobType, entityId, runningStatus, completionStatus, excludeChildJobs, pageNumber, ControllerContext.HttpContext.Request);
        }

        [HttpGet]
        [Route("api/jobs/{jobId}/logs")]
        [ProducesResponseType(200, Type = typeof(IEnumerable<JobLog>))]
        public async Task<IActionResult> GetJobLogs([FromRoute] string jobId)
        {
            return await _jobService.GetJobLogs(jobId, ControllerContext.HttpContext.Request);
        }

        [HttpPost]
        [Route("api/jobs/{jobId}/logs")]
        [ProducesResponseType(202)]
        public async Task<IActionResult> UpdateJobStatusLog(JobLogUpdateModel job)
        {
            return await _jobManagementService.AddJobLog(job, ControllerContext.HttpContext.Request);
        }

        /// <summary>
        /// Manual user cancellation of job
        /// Not needed in first phase
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("api/jobs/{jobId}/cancel")]
        [ProducesResponseType(202)]
        public async Task<IActionResult> CancelJob(string jobId)
        {
            return await _jobManagementService.CancelJob(jobId, ControllerContext.HttpContext.Request);
        }
    }
}
