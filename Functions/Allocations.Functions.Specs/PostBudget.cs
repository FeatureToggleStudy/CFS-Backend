using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Allocations.Models.Specs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Allocations.Repository;

namespace Allocations.Functions.Specs
{
    public static partial class PostBudget
    {
        [FunctionName("PostBudget")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req, TraceWriter log)
        {
            var budget = await req.Content.ReadAsAsync<Budget>();

            if (budget == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest,
                    "Please ensure budget is passed in the request body");
            }

            using (var repository = new Repository<Budget>("specs"))
            {
                await repository.CreateAsync(budget);
            }

            return req.CreateResponse(HttpStatusCode.Created);
        }
    }
}
