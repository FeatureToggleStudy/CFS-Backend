﻿using System;
using System.Threading.Tasks;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Scenarios.Interfaces;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Serilog;

namespace CalculateFunding.Functions.Scenarios.ServiceBus
{
    public class OnDataDefinitionChanges
    {
        private readonly ILogger _logger;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly IDatasetDefinitionFieldChangesProcessor _datasetDefinitionFieldChangesProcessor;

        public OnDataDefinitionChanges(
            ILogger logger,
            ICorrelationIdProvider correlationIdProvider,
            IDatasetDefinitionFieldChangesProcessor datasetDefinitionFieldChangesProcessor)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(correlationIdProvider, nameof(correlationIdProvider));
            Guard.ArgumentNotNull(datasetDefinitionFieldChangesProcessor, nameof(datasetDefinitionFieldChangesProcessor));

            _logger = logger;
            _correlationIdProvider = correlationIdProvider;
            _datasetDefinitionFieldChangesProcessor = datasetDefinitionFieldChangesProcessor;
        }

        [FunctionName("on-data-definition-changes")]
        public async Task Run([ServiceBusTrigger(
            ServiceBusConstants.TopicNames.DataDefinitionChanges,
            ServiceBusConstants.TopicSubscribers.UpdateScenarioFieldDefinitionProperties,
            Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] Message message)
        {
            try
            {
                await _datasetDefinitionFieldChangesProcessor.ProcessChanges(message);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, $"An error occurred getting message from topic: {ServiceBusConstants.TopicNames.DataDefinitionChanges}");
                throw;
            }
        }
    }
}
