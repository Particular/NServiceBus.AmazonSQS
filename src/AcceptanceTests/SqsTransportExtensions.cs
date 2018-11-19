namespace NServiceBus.AmazonSQS.AcceptanceTests
{
    using Amazon.S3;
    using Amazon.SQS;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;

    public static class SqsTransportExtensions
    {
        const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";

        public static TransportExtensions<SqsTransport> ConfigureSqsTransport(this TransportExtensions<SqsTransport> transportConfiguration, string queueNamePrefix)
        {
            transportConfiguration
                .ClientFactory(CreateSQSClient)
                .QueueNamePrefix(queueNamePrefix)
                .PreTruncateQueueNamesForAcceptanceTests();

            S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrEmpty(S3BucketName))
            {
                var s3Configuration = transportConfiguration.S3(S3BucketName, S3Prefix);
                s3Configuration.ClientFactory(CreateS3Client);
            }

            return transportConfiguration;
        }

        public const string S3Prefix = "test";
        public static string S3BucketName;

        public static IAmazonSQS CreateSQSClient() => new AmazonSQSClient();

        public static IAmazonS3 CreateS3Client() => new AmazonS3Client();
    }
}
