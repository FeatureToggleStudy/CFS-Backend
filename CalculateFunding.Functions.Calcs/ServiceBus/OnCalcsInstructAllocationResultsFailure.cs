﻿using System;
using System.Threading.Tasks;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Interfaces.Services;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Serilog;

namespace CalculateFunding.Functions.Calcs.ServiceBus
{
    public class OnCalcsInstructAllocationResultsFailure
    {
        private readonly ILogger _logger;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IJobHelperService _jobHelperService;

        public OnCalcsInstructAllocationResultsFailure(
            ILogger logger,
            ICorrelationIdProvider correlationIdProvider,
            IJobHelperService jobHelperService)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(correlationIdProvider, nameof(correlationIdProvider));
            Guard.ArgumentNotNull(jobHelperService, nameof(jobHelperService));

            _logger = logger;
            _correlationIdProvider = correlationIdProvider;
            _jobHelperService = jobHelperService;
        }

        [FunctionName("on-calcs-instruct-allocations-poisoned")]
        public async Task Run([ServiceBusTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisoned, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] Message message)
        {
            try
            {
                _correlationIdProvider.SetCorrelationId(message.GetCorrelationId());
                await _jobHelperService.ProcessDeadLetteredMessage(message);

                _logger.Information("Proccessed instruct generate allocations dead lettered message complete");
            }
            catch (Exception exception)
            {
                _logger.Error(exception, $"An error occurred getting message from queue: {ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisoned}");
                throw;
            }
        }
    }

}
