using Amazon.Runtime;
using Amazon.SQS;

namespace NServiceBus.SQS
{
    internal static class SqsClientFactory
    {
        public static AmazonSQSClient CreateClient(SqsConnectionConfiguration connectionConfiguration)
        {
            return new AmazonSQSClient(new EnvironmentVariablesAWSCredentials(), connectionConfiguration.Region);
        }
    }
}
