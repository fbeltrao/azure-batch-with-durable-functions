using System;
using System.Runtime.Serialization;

namespace Custom.Azure.WebJobs.Extensions.AzureBatch
{
    [Serializable]
    internal class AzureBatchJobException : Exception
    {
        public AzureBatchJobException()
        {
        }

        public AzureBatchJobException(string message) : base(message)
        {
        }

        public AzureBatchJobException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AzureBatchJobException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}