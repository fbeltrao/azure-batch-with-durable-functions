using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System;
using System.Threading.Tasks;

namespace AzureBatchDurableFunctionSample
{
    public static class Activities
    {
        [FunctionName(nameof(WriteResultsToDatabase))]
        public static async Task WriteResultsToDatabase([ActivityTrigger] string[] results)
        {
            Console.WriteLine($"Pretend we wrote {string.Join(",", results)} to database");
            await Task.Delay(200);
        }
    }
}
