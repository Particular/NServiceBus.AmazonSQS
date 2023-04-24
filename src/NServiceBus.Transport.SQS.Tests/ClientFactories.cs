#nullable enable

namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;

    public static class ClientFactories
    {
        public static IAmazonSQS CreateSqsClient(Action<AmazonSQSConfig>? configure = default)
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var config = new AmazonSQSConfig();
            configure?.Invoke(config);
            return new AmazonSQSClient(credentials, config);
        }

        public static IAmazonSimpleNotificationService CreateSnsClient(Action<AmazonSimpleNotificationServiceConfig>? configure = default)
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var config = new AmazonSimpleNotificationServiceConfig();
            configure?.Invoke(config);
            return new AmazonSimpleNotificationServiceClient(credentials, config);
        }

        public static IAmazonS3 CreateS3Client(Action<AmazonS3Config>? configure = default)
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var config = new AmazonS3Config();
            configure?.Invoke(config);
            return new AmazonS3Client(credentials, config);
        }
    }
}