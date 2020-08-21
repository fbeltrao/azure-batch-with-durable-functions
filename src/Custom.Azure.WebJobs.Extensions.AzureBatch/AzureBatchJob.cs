using Microsoft.Azure.Batch;
using System.Collections.Generic;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    public class AzureBatchJob
    {
        public ImageReference ImageReference { get; set; }
        public string NodeAgentSkuId { get; set; }

        public string JobId { get; set; }

        readonly List<AzureBatchTask> _tasks = new List<AzureBatchTask>();
        public IList<AzureBatchTask> Tasks => _tasks;

        public string PoolId { get; set; }
        public string PoolVMSize { get; set; }
        public int? PoolNodeCount { get; set; }
        public int? PoolLowPriorityNodeCount { get; set; }
        public string DurableFunctionInstanceId { get; set; }

    }
}
