using Amazon.Runtime;
using Amazon.SQS;

namespace NServiceBus.SQS
{
    internal static class SqsClientFactory
    {
        public static IAmazonSQS CreateClient(SqsConnectionConfiguration connectionConfiguration)
        {
            return new AmazonSQSClient(new EnvironmentVariablesAWSCredentials(), connectionConfiguration.Region);
        }
    }
}
