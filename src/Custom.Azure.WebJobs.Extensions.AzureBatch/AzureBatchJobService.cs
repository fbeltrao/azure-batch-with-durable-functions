using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    sealed class AzureBatchJobService : IDisposable, IAzureBatchJobService
    {
        private readonly Lazy<BatchClient> _batchClient;
        private readonly AzureBatchConfiguration _azureBatchConfig;
        private readonly AzureBatchAttribute _attribute;
        private readonly ILogger<AzureBatchJobService> _logger;
        const string STDOUT_FILE_PATH = "stdout.txt";

        internal AzureBatchJobService(AzureBatchConfiguration azureBatchConfiguration, AzureBatchAttribute attribute, ILogger<AzureBatchJobService> logger)
        {
            _azureBatchConfig = azureBatchConfiguration;
            _attribute = attribute;
            _logger = logger;
            _batchClient = new Lazy<BatchClient>(() =>
            {
                var cred = new BatchSharedKeyCredentials(_attribute.AccountUrl, _attribute.AccountName, _attribute.AccountKey);
                return BatchClient.Open(cred);
            });
        }

        public async Task<Microsoft.Azure.Batch.Common.JobState?> GetJobStateAsync(string jobId)
        {
            try
            {
                var batchClient = _batchClient.Value;
                var job = await batchClient.JobOperations.GetJobAsync(jobId);
                return job.State;
            }
            catch (Exception ex)
            {
                throw new AzureBatchJobException($"Error getting job {jobId} state", ex);
            }
        }

        public async Task<string> GetStdOutStringAsync(string jobId, string taskId)
        {
            try
            {
                var batchClient = _batchClient.Value;
                var result = await batchClient.JobOperations.CopyNodeFileContentToStringAsync(jobId, taskId, STDOUT_FILE_PATH);
                return result;
            }
            catch (Exception ex)
            {
                throw new AzureBatchJobException($"Error getting standard out from job={jobId}, task={taskId}", ex);
            }
        }

        public async Task DeletePoolAsync()
        {
            var poolId = BatchConfigurationHelper.GetPoolId(_azureBatchConfig, _attribute);
            try
            {
                var batchClient = _batchClient.Value;
                await batchClient.PoolOperations.DeletePoolAsync(poolId);
            }
            catch (Exception ex)
            {
                throw new AzureBatchJobException($"Error deleting pool {poolId}", ex);
            }
        }

        public void Dispose()
        {
            if (_batchClient.IsValueCreated)
            {
                _batchClient.Value.Dispose();
            }
        }
    }
}
