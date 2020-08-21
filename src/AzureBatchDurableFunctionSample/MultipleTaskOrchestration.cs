using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Custom.Azure.WebJobs.Extensions.AzureBatch;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AzureBatchDurableFunctionSample
{
    public static class MultipleTaskOrchestration
    {
        [FunctionName(nameof(MultipleTaskOrchestrator))]
        public static async Task<string[]> MultipleTaskOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new[] { "Tokio", "Seattle", "London" };
            await context.CallActivityAsync(nameof(SayMultipleHelloInAzureBatch), tasks);

            await context.WaitForBatchJobAsync();

            var responses = await context.CallActivityAsync<string[]>(nameof(GetMultipleTaskStdoutAsync), tasks);

            await context.CallActivityAsync(nameof(Activities.WriteResultsToDatabase), responses);

            return responses;
        }

        [FunctionName(nameof(GetMultipleTaskStdoutAsync))]
        public static async Task<string[]> GetMultipleTaskStdoutAsync(
            [ActivityTrigger] IDurableActivityContext context,
            [AzureBatch(AccountKey = "%BATCH_ACCOUNTKEY%", AccountName = "%BATCH_ACCOUNT_NAME%", AccountUrl = "%BATCH_ACCOUNT_URL%")] IAzureBatchJobService azureBatchJobService)
        {
            var taskIds = context.GetInput<string[]>();
            var result = new List<string>();
            foreach (var taskId in taskIds)
            {
                result.Add(await azureBatchJobService.GetStdOutStringAsync(context.GetJobId(), taskId));
            }

            return result.ToArray();
        }

        [FunctionName(nameof(SayMultipleHelloInAzureBatch))]
        public static void SayMultipleHelloInAzureBatch(
            [ActivityTrigger] IDurableActivityContext createJobContext,
            [AzureBatch(
                AccountKey ="%BATCH_ACCOUNTKEY%",
                AccountName = "%BATCH_ACCOUNT_NAME%",
                AccountUrl = "%BATCH_ACCOUNT_URL%",
                PoolId = "DedicatedLowPriorityPool1",
                PoolNodeCount = 0,
                PoolLowPriorityNodeCount = 1,
                PoolVMSize = "STANDARD_A1_v2")] out AzureBatchJob batchJob)
        {
            var inputParameters = createJobContext.GetInput<string[]>();
            batchJob = createJobContext.CreateAzureBatchJob();
            batchJob.ImageReference = new Microsoft.Azure.Batch.ImageReference(
                publisher: "MicrosoftWindowsServer",
                offer: "WindowsServer",
                sku: "2016-datacenter-smalldisk",
                version: "latest");
            batchJob.NodeAgentSkuId = "batch.node.windows amd64";

            foreach (var input in inputParameters)
            {
                batchJob.Tasks.Add(new AzureBatchTask
                {
                    CommandLine = $"cmd /c echo Saying hello to {input}.",
                    TaskId = input
                });
            }
        }

        [FunctionName(nameof(MultipleTaskOrchestratorHttpStart))]
        public static async Task<HttpResponseMessage> MultipleTaskOrchestratorHttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "tasks/multipleTasks")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(MultipleTaskOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}