﻿using System;
using System.Threading.Tasks;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Interfaces.Services;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CalculateFunding.Functions.Calcs.ServiceBus
{
    public static class OnCalcsInstructAllocationResultsFailure
    {
        [FunctionName("on-calcs-instruct-allocations-poisoned")]
        public static async Task Run([ServiceBusTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisoned, Connection = ServiceBusConstants.ConnectionStringConfigurationKey)] Message message)
        {
            IConfigurationRoot config = ConfigHelper.AddConfig();

            using (IServiceScope scope = IocConfig.Build(config).CreateScope())
            {
                ICorrelationIdProvider correlationIdProvider = scope.ServiceProvider.GetService<ICorrelationIdProvider>();
                IJobHelperService jobHelperService = scope.ServiceProvider.GetService<IJobHelperService>();
                Serilog.ILogger logger = scope.ServiceProvider.GetService<Serilog.ILogger>();

                try
                {
                    correlationIdProvider.SetCorrelationId(message.GetCorrelationId());
                    await jobHelperService.ProcessDeadLetteredMessage(message);

                    logger.Information("Proccessed instruct generate allocations dead lettered message complete");
                }
                catch (Exception exception)
                {
                    logger.Error(exception, $"An error occurred getting message from queue: {ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisoned}");
                    throw;
                }

            }
        }
    }

}
