using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    public sealed class AzureBatchJobAsyncCollector : IAsyncCollector<AzureBatchJob>, IDisposable
    {
        private readonly AzureBatchConfiguration _azureBatchConfiguration;
        private readonly AzureBatchAttribute _azureBatchAttribute;
        private readonly ILogger<AzureBatchJobAsyncCollector> _logger;
        private Lazy<BatchClient> _batchClient;

        public AzureBatchJobAsyncCollector(AzureBatchConfiguration azureBatchConfiguration, AzureBatchAttribute azureBatchAttribute, ILogger<AzureBatchJobAsyncCollector> logger)
        {            
            _azureBatchConfiguration = azureBatchConfiguration;
            _azureBatchAttribute = azureBatchAttribute;
            _logger = logger;
            _batchClient = new Lazy<BatchClient>(() =>
            {
                var cred = new BatchSharedKeyCredentials(_azureBatchAttribute.AccountUrl, _azureBatchAttribute.AccountName, _azureBatchAttribute.AccountKey);
                return BatchClient.Open(cred);
            });
        }

        string GetAzureFunctionHostURL()
        {
            var result = Environment.GetEnvironmentVariable("CUSTOM_FUNCTION_HOST");
            if (string.IsNullOrEmpty(result))
            {
                result = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
                if (!result.StartsWith("http"))
                {
                    if (result.Contains("localhost"))
                    {
                        result = "http://" + result;
                    }
                    else
                    {
                        result = "https://" + result;
                    }
                }
            }

            return result;            
        }

        public async Task AddAsync(AzureBatchJob item, CancellationToken cancellationToken = default)
        {
            var batchClient = _batchClient.Value;

            var isRunningAsDurableFunction = !string.IsNullOrEmpty(item.DurableFunctionInstanceId);
            var jobId = await CreateJobAsync(item, useTaskDependencies: isRunningAsDurableFunction);

            var imageReference = CreateImageReference(item);
            var vmConfiguration = CreateVirtualMachineConfiguration(imageReference, item);
            await CreateBatchPoolAsync(vmConfiguration, item);

            var cloudTasks = new List<CloudTask>();
            cloudTasks.AddRange(item.Tasks.Select(x => CreateCloudTask(x)));

            // Add a event raiser if a durable function instance id was provided
            if (isRunningAsDurableFunction)
            {
                var eventRaiserTask = CreateEventRaiserTask(item, jobId);
                cloudTasks.Add(eventRaiserTask);
            }

            await batchClient.JobOperations.AddTaskAsync(jobId, cloudTasks);
        }

        private CloudTask CreateEventRaiserTask(AzureBatchJob item, string jobId)
        {
            var durableFunctionInstanceId = item.DurableFunctionInstanceId; 
            var jobFinishedJobName = AzureBatchOrchestrationContextExtensions.GetJobFinishedEventName(jobId);
            var cmd = $"powershell -Command \"&{{$done=@{{ done=1 }}; $json=$done | ConvertTo-Json; Invoke-WebRequest -ContentType 'application/json' -Uri {GetAzureFunctionHostURL()}/runtime/webhooks/durabletask/instances/{durableFunctionInstanceId}/raiseEvent/{jobFinishedJobName} -Method POST -Body $json }}";

            var cloudTask = new CloudTask(
                "durable-function-event-raiser",
                cmd);
            
            cloudTask.DependsOn = TaskDependencies.OnIds(item.Tasks.Select(x => x.TaskId));

            return cloudTask;
        }

        private async Task<string> CreateJobAsync(AzureBatchJob item, bool useTaskDependencies)
        {
            var jobId = (!string.IsNullOrEmpty(item.DurableFunctionInstanceId)) ?
                AzureBatchOrchestrationContextExtensions.GetJobId(item.DurableFunctionInstanceId, item.JobId) :
                item.JobId;
            
            try
            {
                var batchClient = _batchClient.Value;
                var job = batchClient.JobOperations.CreateJob();
                job.Id = jobId;
                job.UsesTaskDependencies = useTaskDependencies;
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = GetPoolId(item) };              

                await job.CommitAsync();
            }
            catch (BatchException ex)
            {
                switch (ex.RequestInformation?.BatchError?.Code)
                {
                    case BatchErrorCodeStrings.JobExists:
                        _logger.LogWarning("The job {jobId} already existed when we tried to create it", item.JobId);
                        break;

                    case BatchErrorCodeStrings.JobCompleted:
                        _logger.LogWarning("The job {jobId} already completed when we tried to create it", item.JobId);
                        break;

                    default:
                        throw;
                }
            }

            return jobId;
        }

        private async Task CreateBatchPoolAsync(VirtualMachineConfiguration vmConfiguration, AzureBatchJob azureBatchJob)
        {
            var poolId = GetPoolId(azureBatchJob);
            try
            {
                var batchClient = _batchClient.Value;
                CloudPool pool = batchClient.PoolOperations.CreatePool(
                    poolId: poolId,
                    targetDedicatedComputeNodes: GetPoolNodeCount(azureBatchJob),
                    targetLowPriorityComputeNodes: GetPoolLowPriorityNodeCount(azureBatchJob),
                    virtualMachineSize: GetPoolVMSize(azureBatchJob),
                    virtualMachineConfiguration: vmConfiguration);

                await pool.CommitAsync();
            }
            catch (BatchException be)
            {
                // Accept the specific error code PoolExists as that is expected if the pool already exists
                if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.PoolExists)
                {
                    _logger.LogWarning("The pool {poolId} already existed when we tried to create it", poolId);
                }
                else
                {
                    throw; // Any other exception is unexpected
                }
            }
        }

        private string GetPoolVMSize(AzureBatchJob azureBatchJob)
        {
            return azureBatchJob.PoolVMSize ?? BatchConfigurationHelper.GetVmSize(_azureBatchConfiguration, _azureBatchAttribute);
        }

        private int GetPoolNodeCount(AzureBatchJob azureBatchJob)
        {
            return azureBatchJob.PoolNodeCount ?? BatchConfigurationHelper.GetPoolNodeCount(_azureBatchConfiguration, _azureBatchAttribute);
        }
        private int GetPoolLowPriorityNodeCount(AzureBatchJob azureBatchJob)
        {
            return azureBatchJob.PoolLowPriorityNodeCount ?? BatchConfigurationHelper.GetPoolLowPriorityNodeCount(_azureBatchConfiguration, _azureBatchAttribute);
        }

        private string GetPoolId(AzureBatchJob item)
        {
            return item.PoolId ?? BatchConfigurationHelper.GetPoolId(_azureBatchConfiguration, _azureBatchAttribute); ;
        }

        private CloudTask CreateCloudTask(AzureBatchTask src)
        {
            var cloudTask = new CloudTask(src.TaskId, src.CommandLine);
            return cloudTask;
        }

        private VirtualMachineConfiguration CreateVirtualMachineConfiguration(ImageReference imageReference, AzureBatchJob item)
        {
            return new VirtualMachineConfiguration(
                imageReference: imageReference,
                nodeAgentSkuId: item.NodeAgentSkuId ?? _azureBatchAttribute.NodeAgentSkuId);
        }

        private ImageReference CreateImageReference(AzureBatchJob item)
        {
            return item.ImageReference ?? _azureBatchAttribute.ImageReference;
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            CloseBatchClient();

            return Task.CompletedTask;
        }

        private void CloseBatchClient()
        {
            if (_batchClient.IsValueCreated)
            {
                _batchClient.Value.Dispose();
            }
        }

        public void Dispose()
        {
            CloseBatchClient();
        }
    }
}
