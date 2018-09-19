﻿using System.Globalization;
using System.Threading.Tasks;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Services.Core.Constants;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Datasets
    {
        [FunctionName("on-dataset-event")]
        public static async Task RunPublishProviderResults([QueueTrigger(ServiceBusConstants.QueueNames.ProcessDataset, Connection = "AzureConnectionString")] string item, TraceWriter log)
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            Message message = Helpers.ConvertToMessage<Dataset>(item);

            await Functions.Datasets.ServiceBus.OnDatasetEvent.Run(message);

            log.Info($"C# Queue trigger function processed: {item}");
        }

        [FunctionName("on-dataset-validation-event")]
        public static async Task RunValidateDatasetEvent([QueueTrigger(ServiceBusConstants.QueueNames.ValidateDataset, Connection = "AzureConnectionString")] string item, TraceWriter log)
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB");
            Message message = Helpers.ConvertToMessage<GetDatasetBlobModel>(item);

            await Functions.Datasets.ServiceBus.OnDatasetValidationEvent.Run(message);

            log.Info($"C# Queue trigger function processed: {item}");
        }
    }
}
