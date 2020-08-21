using System.Net.Http;
using System.Threading.Tasks;
using Custom.Azure.WebJobs.Extensions.AzureBatch;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace AzureBatchDurableFunctionSample
{
    public static class MultipleJobOrchestration
    {
        [FunctionName(nameof(MultipleJobOrchestrator))]
        public static async Task<string[]> MultipleJobOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            // 1. Create 3 jobs in Azure Batch
            await Task.WhenAll(
                context.CallActivityAsync(nameof(SayHelloInAzureBatch), "Tokio"),
                context.CallActivityAsync(nameof(SayHelloInAzureBatch), "Seattle"),
                context.CallActivityAsync(nameof(SayHelloInAzureBatch), "London")
                );

            // 2. Wait for all of them to be completed
            // The final task of the job will raise an event to the DurableFunction, indicating that the job is completed
            await Task.WhenAll(
                context.WaitForBatchJobAsync("Tokio"),
                context.WaitForBatchJobAsync("Seattle"),
                context.WaitForBatchJobAsync("London")
                );


            // 3. Gets the stdout from each job
            var responses = new[]
            {
                await context.CallActivityAsync<string>(nameof(GetJobStdoutAsync), "Tokio"),
                await context.CallActivityAsync<string>(nameof(GetJobStdoutAsync), "Seattle"),
                await context.CallActivityAsync<string>(nameof(GetJobStdoutAsync), "London"),
            };

            // 4. Do something with results
            await context.CallActivityAsync(nameof(Activities.WriteResultsToDatabase), responses);

            return responses;
        }

        [FunctionName(nameof(GetJobStdoutAsync))]
        public static Task<string> GetJobStdoutAsync(
            [ActivityTrigger] IDurableActivityContext context,
            [AzureBatch(AccountKey = "%BATCH_ACCOUNTKEY%", AccountName = "%BATCH_ACCOUNT_NAME%", AccountUrl = "%BATCH_ACCOUNT_URL%")] IAzureBatchJobService azureBatchJobService)
        {
            var jobId = context.GetInput<string>();
            return azureBatchJobService.GetStdOutStringAsync(context.GetJobId(jobId), "1");
        }

        [FunctionName(nameof(SayHelloInAzureBatch))]
        public static void SayHelloInAzureBatch(
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
            var inputParameter = createJobContext.GetInput<string>();
            batchJob = createJobContext.CreateAzureBatchJob();
            batchJob.JobId = inputParameter;
            batchJob.ImageReference = new Microsoft.Azure.Batch.ImageReference(
                publisher: "MicrosoftWindowsServer",
                offer: "WindowsServer",
                sku: "2016-datacenter-smalldisk",
                version: "latest");
            batchJob.NodeAgentSkuId = "batch.node.windows amd64";

            batchJob.Tasks.Add(new AzureBatchTask
            {
                CommandLine = $"cmd /c echo Saying hello to {inputParameter}.",
                TaskId = "1"
            });
        }

        [FunctionName(nameof(MultipleJobOrchestrationHttpStart))]
        public static async Task<HttpResponseMessage> MultipleJobOrchestrationHttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "tasks/multipleJobs")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(MultipleJobOrchestrator), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}