namespace NServiceBus.AmazonSQS.AcceptanceTests
{
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;

    public static class SqsTransportExtensions
    {
        const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";

        public static TransportExtensions<SqsTransport> ConfigureSqsTransport(this TransportExtensions<SqsTransport> transportConfiguration, string namePrefix)
        {
            transportConfiguration
                .ClientFactory(CreateSQSClient)
                .ClientFactory(CreateSnsClient)
                .QueueNamePrefix(namePrefix)
                .TopicNamePrefix(namePrefix)
                .PreTruncateQueueNamesForAcceptanceTests()
                .PreTruncateTopicNamesForAcceptanceTests();

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

        public static IAmazonSQS CreateSQSClient()
        {
            var config = new AmazonSQSConfig();
#if NETSTANDARD
            config.CacheHttpClient = true;
            config.HttpClientCacheSize = 1;
#endif

            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSQSClient(credentials, config);
        }

        public static IAmazonSimpleNotificationService CreateSnsClient()
        {
            var config = new AmazonSimpleNotificationServiceConfig();
#if NETSTANDARD
            config.CacheHttpClient = true;
            config.HttpClientCacheSize = 1;
#endif

            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSimpleNotificationServiceClient(credentials, config);
        }

        public static IAmazonS3 CreateS3Client()
        {
            var config = new AmazonS3Config();
#if NETSTANDARD
            config.CacheHttpClient = true;
            config.HttpClientCacheSize = 1;
#endif

            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonS3Client(credentials, config);
        }
    }
}
