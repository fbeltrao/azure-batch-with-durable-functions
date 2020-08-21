using Microsoft.Azure.Batch;
using Microsoft.Azure.WebJobs.Description;
using System;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [Binding]
    public class AzureBatchAttribute : Attribute
    {
        [AutoResolve]
        public string AccountUrl { get; set; }
        
        [AutoResolve]
        public string AccountName { get; set; }
        
        [AutoResolve]
        public string AccountKey { get; set; }
        public ImageReference ImageReference { get; set; }

        [AutoResolve]
        public string NodeAgentSkuId { get; set; }

        [AutoResolve]
        public string PoolId { get; set; }

        [AutoResolve]
        public string PoolVMSize { get; set; }

        public int PoolNodeCount { get; set; } = 1;
        public int PoolLowPriorityNodeCount { get; set; }
    }
}
