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

            var s3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrEmpty(s3BucketName))
            {
                var s3Configuration = transportConfiguration.S3(s3BucketName, "test");
                s3Configuration.ClientFactory(CreateS3Client);
            }

            return transportConfiguration;
        }

#if NET452
        public static IAmazonSQS CreateSQSClient() => new AmazonSQSClient();
#endif
#if NETCOREAPP2_0
        public static IAmazonSQS CreateSQSClient() => new AmazonSQSClient(new AmazonSQSConfig { CacheHttpClient = false });
#endif
        public static IAmazonS3 CreateS3Client() => new AmazonS3Client();
    }
}
