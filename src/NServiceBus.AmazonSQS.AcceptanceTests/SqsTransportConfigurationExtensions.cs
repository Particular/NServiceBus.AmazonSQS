namespace NServiceBus.AmazonSQS.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;

    public static class SqsTransportConfigurationExtensions
    {
        public static TransportExtensions<SqsTransport> ConfigureSqsTransport(this TransportExtensions<SqsTransport> transportConfiguration, string queueNamePrefix)
        {
            var region = EnvironmentHelper.GetEnvironmentVariable(RegionEnvironmentVariableName) ?? "ap-southeast-2";

            transportConfiguration
                .Region(region)
                .QueueNamePrefix(queueNamePrefix)
                .PreTruncateQueueNamesForAcceptanceTests();

            S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrEmpty(S3BucketName))
            {
                transportConfiguration.S3BucketForLargeMessages(S3BucketName, S3Prefix);
            }

            var nativeDeferralRaw = EnvironmentHelper.GetEnvironmentVariable(NativeDeferralEnvironmentVariableName);
            var validValue = bool.TryParse(nativeDeferralRaw, out var nativeDeferral);
            if (validValue && nativeDeferral)
            {
                transportConfiguration.NativeDeferral();
            }

            return transportConfiguration;
        }

        const string RegionEnvironmentVariableName = "NServiceBus.AmazonSQS.Region";
        const string S3BucketEnvironmentVariableName = "NServiceBus.AmazonSQS.S3Bucket";
        const string NativeDeferralEnvironmentVariableName = "NServiceBus.AmazonSQS.NativeDeferral";

        public const string S3Prefix = "test";
        public static string S3BucketName;
    }
}