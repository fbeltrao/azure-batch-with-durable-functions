using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading.Tasks;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    public static class AzureBatchOrchestrationContextExtensions
    {
        internal static string GetJobFinishedEventName(string jobId) => $"batchjob-finished-{jobId}";
        internal static string GetJobId(string instanceId, string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
                return instanceId;

            return string.Concat(instanceId, "_", jobId);
        }

        public static async Task WaitForBatchJobAsync(this IDurableOrchestrationContext context)
        {
            var eventName = GetJobFinishedEventName(context.InstanceId);
            await context.WaitForExternalEvent(eventName);
        }

        public static async Task WaitForBatchJobAsync(this IDurableOrchestrationContext context, string jobId)
        {
            var eventName = GetJobFinishedEventName(jobId);
            await context.WaitForExternalEvent(eventName);
        }

        public static string GetJobId(this IDurableActivityContext context, string userProvidedJobId = null)
            => GetJobId(context.InstanceId, userProvidedJobId);

        public static AzureBatchJob CreateAzureBatchJob(this IDurableActivityContext context)
        {
            return new AzureBatchJob
            {
                DurableFunctionInstanceId = context.InstanceId,
            };
        }
    }
}
