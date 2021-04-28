namespace NServiceBus.AcceptanceTests
{
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using ScenarioDescriptors;

    public static class SqsTransportExtensions
    {
        const string S3BucketEnvironmentVariableName = "NSERVICEBUS_AMAZONSQS_S3BUCKET";

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
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSQSClient(credentials);
        }

        public static IAmazonSimpleNotificationService CreateSnsClient()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSimpleNotificationServiceClient(credentials);
        }

        public static IAmazonS3 CreateS3Client()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonS3Client(credentials);
        }
    }
}
