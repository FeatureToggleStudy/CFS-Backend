﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models;
using CalculateFunding.Models.Health;
using CalculateFunding.Models.Jobs;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Interfaces.ServiceBus;
using CalculateFunding.Services.Core.Interfaces.Services;
using CalculateFunding.Services.Jobs.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Serilog;

namespace CalculateFunding.Services.Jobs
{
    public class JobManagementService : IJobManagementService, IHealthChecker
    {
        private readonly IJobRepository _jobRepository;
        private readonly INotificationService _notificationService;
        private readonly IJobDefinitionsService _jobDefinitionsService;
        private readonly Polly.Policy _jobsRepositoryPolicy;
        private readonly Polly.Policy _jobsRepositoryNonAsyncPolicy;
        private readonly Polly.Policy _jobDefinitionsRepositoryPolicy;
        private readonly Polly.Policy _messengerServicePolicy;
        private readonly ILogger _logger;
        private readonly IValidator<CreateJobValidationModel> _createJobValidator;
        private readonly IMessengerService _messengerService;

        public JobManagementService(
            IJobRepository jobRepository,
            INotificationService notificationService,
            IJobDefinitionsService jobDefinitionsService,
            IJobsResiliencePolicies resilliencePolicies,
            ILogger logger,
            IValidator<CreateJobValidationModel> createJobValidator,
            IMessengerService messengerService)
        {
            Guard.ArgumentNotNull(jobRepository, nameof(jobRepository));
            Guard.ArgumentNotNull(notificationService, nameof(notificationService));
            Guard.ArgumentNotNull(jobDefinitionsService, nameof(jobDefinitionsService));
            Guard.ArgumentNotNull(resilliencePolicies, nameof(resilliencePolicies));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(createJobValidator, nameof(createJobValidator));
            Guard.ArgumentNotNull(messengerService, nameof(messengerService));

            _jobRepository = jobRepository;
            _notificationService = notificationService;
            _jobDefinitionsService = jobDefinitionsService;
            _jobsRepositoryPolicy = resilliencePolicies.JobRepository;
            _jobDefinitionsRepositoryPolicy = resilliencePolicies.JobDefinitionsRepository;
            _jobsRepositoryNonAsyncPolicy = resilliencePolicies.JobRepositoryNonAsync;
            _logger = logger;
            _createJobValidator = createJobValidator;
            _messengerService = messengerService;
            _messengerServicePolicy = resilliencePolicies.MessengerServicePolicy;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            ServiceHealth jobsRepoHealth = await ((IHealthChecker)_jobRepository).IsHealthOk();

            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(JobManagementService)
            };
            health.Dependencies.AddRange(jobsRepoHealth.Dependencies);
            return health;
        }

        public async Task<IActionResult> CreateJobs(IEnumerable<JobCreateModel> jobs, HttpRequest request)
        {
            Guard.ArgumentNotNull(jobs, nameof(jobs));
            Guard.ArgumentNotNull(request, nameof(request));

            if (!jobs.Any())
            {
                _logger.Warning("Empty collection of job create models was provided");

                return new BadRequestObjectResult("Empty collection of job create models was provided");
            }

            IEnumerable<JobDefinition> jobDefinitions = await _jobDefinitionsService.GetAllJobDefinitions();

            if (jobDefinitions.IsNullOrEmpty())
            {
                _logger.Error("Failed to retrieve job definitions");
                return new InternalServerErrorResult("Failed to retrieve job definitions");
            }

            IList<ValidationResult> validationResults = new List<ValidationResult>();

            Reference user = request.GetUser();

            //ensure all jobs in batch have the correct job definition
            foreach (JobCreateModel jobCreateModel in jobs)
            {
                Guard.IsNullOrWhiteSpace(jobCreateModel.JobDefinitionId, nameof(jobCreateModel.JobDefinitionId));

                JobDefinition jobDefinition = jobDefinitions?.FirstOrDefault(m => m.Id == jobCreateModel.JobDefinitionId);

                if (jobDefinition == null)
                {
                    _logger.Warning($"A job definition could not be found for id: {jobCreateModel.JobDefinitionId}");

                    return new PreconditionFailedResult($"A job definition could not be found for id: {jobCreateModel.JobDefinitionId}");
                }

                if (!jobCreateModel.Properties.ContainsKey("sfa-correlationId"))
                {
                    jobCreateModel.Properties.Add("sfa-correlationId", request.GetCorrelationId());
                }

                CreateJobValidationModel createJobValidationModel = new CreateJobValidationModel
                {
                    JobCreateModel = jobCreateModel,
                    JobDefinition = jobDefinition
                };

                ValidationResult validationResult = _createJobValidator.Validate(createJobValidationModel);
                if (validationResult != null && !validationResult.IsValid)
                {
                    validationResults.Add(validationResult);
                }
            }

            if (validationResults.Any())
            {
                return new BadRequestObjectResult(validationResults);
            }

            IList<Job> createdJobs = new List<Job>();

            foreach (JobCreateModel job in jobs)
            {
                Guard.ArgumentNotNull(job.Trigger, nameof(job.Trigger));

                JobDefinition jobDefinition = jobDefinitions.First(m => m.Id == job.JobDefinitionId);


                if (string.IsNullOrWhiteSpace(job.InvokerUserId) || string.IsNullOrWhiteSpace(job.InvokerUserDisplayName))
                {
                    job.InvokerUserId = user.Id;
                    job.InvokerUserDisplayName = user.Name;
                }

                Job newJobResult = await CreateJob(job);

                if (newJobResult == null)
                {
                    _logger.Error($"Failed to create a job for job definition id: {job.JobDefinitionId}");
                    return new InternalServerErrorResult($"Failed to create a job for job definition id: {job.JobDefinitionId}");
                }

                createdJobs.Add(newJobResult);

                await CheckForSupersededAndCancelOtherJobs(newJobResult, jobDefinition);

                await QueueNewJob(newJobResult, jobDefinition);

                JobNotification jobNotification = CreateJobNotificationFromJob(newJobResult);

                await _notificationService.SendNotification(jobNotification);
            }

            return new OkObjectResult(createdJobs);
        }

        public async Task<IActionResult> AddJobLog(string jobId, JobLogUpdateModel jobLogUpdateModel)
        {
            Guard.IsNullOrWhiteSpace(jobId, nameof(jobId));
            Guard.ArgumentNotNull(jobLogUpdateModel, nameof(jobLogUpdateModel));

            Job job = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.GetJobById(jobId));

            if (job == null)
            {
                _logger.Error($"A job could not be found for job id: '{jobId}'");

                return new NotFoundObjectResult($"A job could not be found for job id: '{jobId}'");
            }

            bool saveJob = false;

            if (jobLogUpdateModel.CompletedSuccessfully.HasValue)
            {
                job.Completed = DateTimeOffset.UtcNow;
                job.RunningStatus = RunningStatus.Completed;
                job.CompletionStatus = jobLogUpdateModel.CompletedSuccessfully.Value ? CompletionStatus.Succeeded : CompletionStatus.Failed;
                job.Outcome = jobLogUpdateModel.Outcome;
                saveJob = true;
            }
            else
            {
                if (job.RunningStatus != RunningStatus.InProgress)
                {
                    job.RunningStatus = RunningStatus.InProgress;
                    saveJob = true;
                }
            }

            if (saveJob)
            {
                HttpStatusCode statusCode = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.UpdateJob(job));

                if (!statusCode.IsSuccess())
                {
                    _logger.Error($"Failed to update job id: '{jobId}' with status code '{(int)statusCode}'");
                    return new InternalServerErrorResult($"Failed to update job id: '{jobId}' with status code '{(int)statusCode}'");
                }
            }

            JobLog jobLog = new JobLog
            {
                Id = Guid.NewGuid().ToString(),
                JobId = jobId,
                ItemsProcessed = jobLogUpdateModel.ItemsProcessed,
                ItemsSucceeded = jobLogUpdateModel.ItemsSucceeded,
                ItemsFailed = jobLogUpdateModel.ItemsFailed,
                Outcome = jobLogUpdateModel.Outcome,
                CompletedSuccessfully = jobLogUpdateModel.CompletedSuccessfully,
                Timestamp = DateTimeOffset.UtcNow
            };

            HttpStatusCode createJobLogStatus = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.CreateJobLog(jobLog));

            if (!createJobLogStatus.IsSuccess())
            {
                _logger.Error($"Failed to create a job log for job id: '{jobId}'");
                throw new Exception($"Failed to create a job log for job id: '{jobId}'");
            }

            await SendJobLogNotification(job, jobLog);

            return new OkObjectResult(jobLog);
        }

        /// <summary>
        /// Cancel job based on internal state management conditions
        /// </summary>
        /// <param name="jobId">Job ID</param>
        /// <returns></returns>
        public async Task CancelJob(string jobId)
        {
            // Set running status to Cancelled and CompletionStatus to Fail

            // Send notification after status logged
            await _notificationService.SendNotification(new JobNotification());

            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Cancel job based on user input - may not be needed for first phase
        /// </summary>
        /// <param name="jobId">Job ID</param>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<IActionResult> CancelJob(string jobId, HttpRequest request)
        {
            // Send notification after status logged
            await _notificationService.SendNotification(new JobNotification());

            throw new System.NotImplementedException();
        }

        public async Task SupersedeJob(Job runningJob, string replacementJobId)
        {
            runningJob.Completed = DateTimeOffset.UtcNow;
            runningJob.CompletionStatus = CompletionStatus.Superseded;
            runningJob.SupersededByJobId = replacementJobId;
            runningJob.RunningStatus = RunningStatus.Completed;

            HttpStatusCode statusCode = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.UpdateJob(runningJob));

            if (statusCode.IsSuccess())
            {
                JobNotification jobNotification = CreateJobNotificationFromJob(runningJob);

                await _notificationService.SendNotification(jobNotification);
            }
            else
            {
                _logger.Error($"Failed to update superseded job, Id: {runningJob.Id}");
            }
        }

        public async Task ProcessJobCompletion(Message message)
        {
            Guard.ArgumentNotNull(message, nameof(message));

            // When a job completes see if the parent job can be completed
            JobNotification jobNotification = message.GetPayloadAsInstanceOf<JobNotification>();

            Guard.ArgumentNotNull(jobNotification, "message payload");

            if (jobNotification.RunningStatus == RunningStatus.Completed)
            {
                if (!message.UserProperties.ContainsKey("jobId"))
                {
                    _logger.Error("Job Notification message has no JobId");
                    return;
                }

                string jobId = message.UserProperties["jobId"].ToString();

                Job job = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.GetJobById(jobId));

                if (job == null)
                {
                    _logger.Error("Could not find job with id {JobId}", jobId);
                    return;
                }

                if (!string.IsNullOrEmpty(job.ParentJobId))
                {
                    IEnumerable<Job> childJobs = _jobsRepositoryNonAsyncPolicy.Execute(() => _jobRepository.GetChildJobsForParent(job.ParentJobId));

                    if (!childJobs.IsNullOrEmpty() && childJobs.All(j => j.RunningStatus == RunningStatus.Completed))
                    {
                        Job parentJob = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.GetJobById(job.ParentJobId));

                        parentJob.Completed = DateTimeOffset.UtcNow;
                        parentJob.RunningStatus = RunningStatus.Completed;
                        parentJob.CompletionStatus = DetermineCompletionStatus(childJobs);
                        parentJob.Outcome = "All child jobs completed";

                        await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.UpdateJob(parentJob));
                        _logger.Information("Parent Job {ParentJobId} of Completed Job {JobId} has been completed because all child jobs are now complete", job.ParentJobId, jobId);

                        await _notificationService.SendNotification(CreateJobNotificationFromJob(parentJob));
                    }
                    else
                    {
                        _logger.Information("Completed Job {JobId} parent {ParentJobId} has in progress child jobs and cannot be completed", jobId, job.ParentJobId);
                    }
                }
                else
                {
                    _logger.Information("Completed Job {JobId} has no parent", jobId);
                }
            }
        }

        public async Task CheckAndProcessTimedOutJobs()
        {
            IEnumerable<Job> nonCompletedJobs = _jobsRepositoryNonAsyncPolicy.Execute(() => _jobRepository.GetNonCompletedJobs());

            if (nonCompletedJobs.IsNullOrEmpty())
            {
                _logger.Information("Zero non completed jobs to process, finished processing timed out jobs");

                return;
            }

            int countOfJobsToProcess = nonCompletedJobs.Count();

            _logger.Information($"{countOfJobsToProcess} non completed jobs to process");

            IEnumerable<JobDefinition> jobDefinitions = await _jobDefinitionsService.GetAllJobDefinitions();

            if (jobDefinitions.IsNullOrEmpty())
            {
                _logger.Error("Failed to retrieve job definitions when processing timed out jobs");
                throw new Exception("Failed to retrieve job definitions when processing timed out jobs");
            }

            foreach(Job job in nonCompletedJobs)
            {
                JobDefinition jobDefinition = jobDefinitions.FirstOrDefault(m => m.Id == job.JobDefinitionId);

                if(jobDefinition == null)
                {
                    _logger.Error($"Failed to find job definition : '{job.JobDefinitionId}' for job id: '{job.Id}'");

                    continue;
                }
                else
                {
                    DateTimeOffset jobStartDate = job.Created;

                    TimeSpan timeout = jobDefinition.Timeout;

                    if(DateTimeOffset.UtcNow > jobStartDate.Add(timeout))
                    {
                        _logger.Information($"Job with id: '{job.Id}' as exceeded its maximum timeout threshold");

                        await TimeoutJob(job);
                    }
                }
            }

        }

        private async Task TimeoutJob(Job runningJob)
        {
            runningJob.Completed = DateTimeOffset.UtcNow;
            runningJob.CompletionStatus = CompletionStatus.TimedOut;
            runningJob.RunningStatus = RunningStatus.Completed;

            HttpStatusCode statusCode = await _jobsRepositoryPolicy.ExecuteAsync(() => _jobRepository.UpdateJob(runningJob));

            if (statusCode.IsSuccess())
            {
                JobNotification jobNotification = CreateJobNotificationFromJob(runningJob);

                await _notificationService.SendNotification(jobNotification);
            }
            else
            {
                _logger.Error($"Failed to update timeout job, Id: '{runningJob.Id}' with status code {(int)statusCode}");
            }
        }

        private CompletionStatus? DetermineCompletionStatus(IEnumerable<Job> jobs)
        {
            if (jobs.Any(j => !j.CompletionStatus.HasValue))
            {
                // There are still some jobs in progress so there is no completion status
                return null;
            }
            else if (jobs.Any(j => j.CompletionStatus == CompletionStatus.TimedOut))
            {
                // At least one job timed out so that is the overall completion status for the group of jobs
                return CompletionStatus.TimedOut;
            }
            else if (jobs.Any(j => j.CompletionStatus == CompletionStatus.Cancelled))
            {
                // At least one job was cancelled so that is the overall completion status for the group of jobs
                return CompletionStatus.Cancelled;
            }
            else if (jobs.Any(j => j.CompletionStatus == CompletionStatus.Superseded))
            {
                // At least one job was superseded so that is the overall completion status for the group of jobs
                return CompletionStatus.Superseded;
            }
            else if (jobs.Any(j => j.CompletionStatus == CompletionStatus.Failed))
            {
                // At least one job failed so that is the overall completion status for the group of jobs
                return CompletionStatus.Failed;
            }
            else
            {
                // Got to here so that must mean all jobs succeeded
                return CompletionStatus.Succeeded;
            }
        }

        private JobNotification CreateJobNotificationFromJob(Job job)
        {
            return new JobNotification
            {
                CompletionStatus = job.CompletionStatus,
                InvokerUserDisplayName = job.InvokerUserDisplayName,
                InvokerUserId = job.InvokerUserId,
                ItemCount = job.ItemCount,
                JobId = job.Id,
                JobType = job.JobDefinitionId,
                ParentJobId = job.ParentJobId,
                Outcome = job.Outcome,
                RunningStatus = job.RunningStatus,
                SpecificationId = job.SpecificationId,
                StatusDateTime = DateTimeOffset.UtcNow,
                SupersededByJobId = job.SupersededByJobId,
                Trigger = job.Trigger
            };
        }

        private async Task CheckForSupersededAndCancelOtherJobs(Job currentJob, JobDefinition jobDefinition)
        {
            if (jobDefinition.SupersedeExistingRunningJobOnEnqueue)
            {
                IEnumerable<Job> runningJobs = _jobsRepositoryNonAsyncPolicy.Execute(() => _jobRepository.GetRunningJobsForSpecificationAndJobDefinitionId(currentJob.SpecificationId, jobDefinition.Id));

                if (!runningJobs.IsNullOrEmpty())
                {
                    foreach (Job runningJob in runningJobs)
                    {
                        await SupersedeJob(runningJob, currentJob.Id);
                    }
                }
            }
        }

        private async Task<Job> CreateJob(JobCreateModel job)
        {
            Job newJob = new Job()
            {
                JobDefinitionId = job.JobDefinitionId,
                InvokerUserId = job.InvokerUserId,
                InvokerUserDisplayName = job.InvokerUserDisplayName,
                ItemCount = job.ItemCount,
                SpecificationId = job.SpecificationId,
                Trigger = job.Trigger,
                ParentJobId = job.ParentJobId,
                CorrelationId = job.CorrelationId,
                Properties = job.Properties,
                MessageBody = job.MessageBody
            };

            Job newJobResult = null;

            try
            {
                newJobResult = await _jobDefinitionsRepositoryPolicy.ExecuteAsync(() => _jobRepository.CreateJob(newJob));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to save new job with definition id {job.JobDefinitionId}");
            }

            return newJobResult;
        }

        private async Task SendJobLogNotification(Job job, JobLog jobLog)
        {
            JobNotification jobNotification = new JobNotification
            {
                JobId = job.Id,
                JobType = job.JobDefinitionId,
                RunningStatus = job.RunningStatus,
                CompletionStatus = job.CompletionStatus,
                SpecificationId = job.SpecificationId,
                InvokerUserDisplayName = job.InvokerUserDisplayName,
                InvokerUserId = job.InvokerUserId,
                ItemCount = job.ItemCount,
                Trigger = job.Trigger,
                ParentJobId = job.ParentJobId,
                SupersededByJobId = job.SupersededByJobId,
                Outcome = jobLog.Outcome,
                StatusDateTime = DateTimeOffset.UtcNow,
                OverallItemsFailed = jobLog.ItemsFailed,
                OverallItemsProcessed = jobLog.ItemsProcessed,
                OverallItemsSucceeded = jobLog.ItemsSucceeded
            };

            await _notificationService.SendNotification(jobNotification);
        }

        private async Task QueueNewJob(Job job, JobDefinition jobDefinition)
        {
            string queueOrTopic = !string.IsNullOrWhiteSpace(jobDefinition.MessageBusQueue) ? jobDefinition.MessageBusQueue : jobDefinition.MessageBusTopic;
            string data = !string.IsNullOrWhiteSpace(job.MessageBody) ? job.MessageBody : null;
            IDictionary<string, string> messageProperties = job.Properties;

            if (messageProperties == null)
            {
                messageProperties = new Dictionary<string, string> { { "jobId", job.Id } };
            }
            else
            {
                if (!messageProperties.ContainsKey("jobId"))
                {
                    messageProperties.Add("jobId", job.Id);
                }
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(jobDefinition.MessageBusQueue))
                {
                    await _messengerService.SendToQueue<string>(jobDefinition.MessageBusQueue, data, messageProperties);
                }
                else
                {
                    await _messengerService.SendToTopic<string>(jobDefinition.MessageBusTopic, data, messageProperties);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to queue job with id: {job.Id} on Queue/topic {queueOrTopic}");
            }
        }
    }
}