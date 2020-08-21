using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(Custom.Azure.WebJobs.Extensions.AzureBatch.Startup))]

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    public class Startup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddExtension<AzureBatchConfiguration>();
        }
    }
}
