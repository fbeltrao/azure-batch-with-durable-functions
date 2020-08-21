namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    internal static class BatchConfigurationHelper
    {

        internal static string GetPoolId(AzureBatchConfiguration config, AzureBatchAttribute attribute)
        {
            return attribute.PoolId ?? config.PoolId;
        }

        internal static string GetVmSize(AzureBatchConfiguration config, AzureBatchAttribute attribute)
        {
            return attribute.PoolVMSize ?? config.PoolVMSize;
        }

        internal static int GetPoolNodeCount(AzureBatchConfiguration config, AzureBatchAttribute attribute)
        {
            if (attribute.PoolNodeCount > 0)
                return attribute.PoolNodeCount;

            return config.PoolNodeCount;
        }

        internal static int GetPoolLowPriorityNodeCount(AzureBatchConfiguration config, AzureBatchAttribute attribute)
        {
            if (attribute.PoolLowPriorityNodeCount > 0)
                return attribute.PoolLowPriorityNodeCount;

            return config.PoolLowPriorityNodeCount;
        }
    }
}
