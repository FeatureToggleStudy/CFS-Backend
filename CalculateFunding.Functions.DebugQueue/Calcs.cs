using System.Threading.Tasks;
using CalculateFunding.Functions.Calcs.ServiceBus;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Calcs
    {
        [FunctionName("on-calc-events-create-draft")]
        public static async Task RunOnCalcsCreateDraftEvent([QueueTrigger(ServiceBusConstants.QueueNames.CreateDraftCalculation, Connection = "AzureConnectionString")] string item, ILogger log)
        {
            using (IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                Message message = Helpers.ConvertToMessage<Calculation>(item);

                OnCalcsCreateDraftEvent function = scope.ServiceProvider.GetService<OnCalcsCreateDraftEvent>();

                await function.Run(message);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }

        [FunctionName("on-calcs-add-data-relationship")]
        public static async Task RunCalcsAddRelationshipToBuildProject([QueueTrigger(ServiceBusConstants.QueueNames.UpdateBuildProjectRelationships, Connection = "AzureConnectionString")] string item, ILogger log)
        {
            using (IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                Message message = Helpers.ConvertToMessage<DatasetRelationshipSummary>(item);

                CalcsAddRelationshipToBuildProject function = scope.ServiceProvider.GetService<CalcsAddRelationshipToBuildProject>();

                await function.Run(message);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }

        [FunctionName("on-calcs-instruct-allocations")]
        public static async Task RunOnCalcsInstructAllocationResults([QueueTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiser, Connection = "AzureConnectionString")] string item, ILogger log)
        {
            using (IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                Message message = Helpers.ConvertToMessage<string>(item);

                OnCalcsInstructAllocationResults function = scope.ServiceProvider.GetService<OnCalcsInstructAllocationResults>();

                await function.Run(message);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }

        [FunctionName("on-calcs-instruct-allocations-poisoned")]
        public static async Task RunOnCalcsInstructAllocationResultsFailure([QueueTrigger(ServiceBusConstants.QueueNames.CalculationJobInitialiserPoisonedLocal, Connection = "AzureConnectionString")] string item, ILogger log)
        {
            using (IServiceScope scope = Functions.Calcs.Startup.RegisterComponents(new ServiceCollection()).CreateScope())
            {
                Message message = Helpers.ConvertToMessage<string>(item);

                OnCalcsInstructAllocationResultsFailure function = scope.ServiceProvider.GetService<OnCalcsInstructAllocationResultsFailure>();

                await function.Run(message);

                log.LogInformation($"C# Queue trigger function processed: {item}");
            }
        }
    }
}
