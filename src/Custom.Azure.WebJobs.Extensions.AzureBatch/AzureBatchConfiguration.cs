using Microsoft.Azure.Batch;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    public class AzureBatchConfiguration : IExtensionConfigProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        public string AccountUrl { get; set; }
        public string AccountName { get; set; }
        public string AccountKey { get; set; }
        public ImageReference ImageReference { get; set; }
        public string NodeAgentSkuId { get; set; }
        public string PoolId { get; set; }
        public string PoolVMSize { get; set; }
        public int PoolNodeCount { get; set; }
        public int PoolLowPriorityNodeCount { get; set; }

        public AzureBatchConfiguration(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public void Initialize(ExtensionConfigContext context)
        {            
            context.AddBindingRule<AzureBatchAttribute>()
                .BindToCollector(attr => new AzureBatchJobAsyncCollector(this, attr, _loggerFactory.CreateLogger<AzureBatchJobAsyncCollector>()));


            // Make the IAzureBatchService bindable
            context.AddBindingRule<AzureBatchAttribute>()
                .BindToInput(ResolveBatchJobService);
        }

        private IAzureBatchJobService ResolveBatchJobService(AzureBatchAttribute attribute)
        {
            return new AzureBatchJobService(this, attribute, _loggerFactory.CreateLogger<AzureBatchJobService>());
        }
    }
}

