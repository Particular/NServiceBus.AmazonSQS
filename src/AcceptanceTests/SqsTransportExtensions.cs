namespace NServiceBus.AmazonSQS.AcceptanceTests
{
    using Amazon.S3;
    using Amazon.SQS;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;

    public static class SqsTransportExtensions
    {
        const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";
        const string NativeDeferralEnvironmentVariableName = "NServiceBus_AmazonSQS_NativeDeferral";

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

            var nativeDeferralRaw = EnvironmentHelper.GetEnvironmentVariable(NativeDeferralEnvironmentVariableName);
            var validValue = bool.TryParse(nativeDeferralRaw, out var nativeDeferral);
            if (validValue && nativeDeferral)
            {
                transportConfiguration.NativeDeferral();
            }

            return transportConfiguration;
        }

        public static IAmazonSQS CreateSQSClient() => new AmazonSQSClient();

        public static IAmazonS3 CreateS3Client() => new AmazonS3Client();
    }
}
