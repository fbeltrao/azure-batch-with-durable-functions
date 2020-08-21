using System.Threading.Tasks;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    public interface IAzureBatchJobService
    {
        Task<string> GetStdOutStringAsync(string jobId, string taskId);
        Task<Microsoft.Azure.Batch.Common.JobState?> GetJobStateAsync(string jobId);
    }
}