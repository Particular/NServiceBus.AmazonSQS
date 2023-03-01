namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;

    public static class ClientFactories
    {
        public static IAmazonSQS CreateSqsClient(Action<AmazonSQSConfig> configure = default)
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var config = Create<AmazonSQSConfig>();
            configure?.Invoke(config);
            return new AmazonSQSClient(credentials, config);
        }

        public static IAmazonSimpleNotificationService CreateSnsClient(Action<AmazonSimpleNotificationServiceConfig> configure = default)
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var config = Create<AmazonSimpleNotificationServiceConfig>();
            configure?.Invoke(config);
            return new AmazonSimpleNotificationServiceClient(credentials, config);
        }

        public static IAmazonS3 CreateS3Client(Action<AmazonS3Config> configure = default)
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            var config = Create<AmazonS3Config>();
            configure?.Invoke(config);
            return new AmazonS3Client(credentials, config);
        }

        // Can be removed once https://github.com/aws/aws-sdk-net/issues/1929 is addressed by the team
        // setting the cache size to 1 will significantly improve the throughput on non-windows OSS while
        // windows had already 1 as the default.
        // There might be other occurrences of setting this setting explicitly in the code base. Make sure to remove them
        // consistently once the issue is addressed. 
        static TConfig Create<TConfig>()
            where TConfig : ClientConfig, new()
        {
#if NET
            var config = new TConfig { HttpClientCacheSize = 1 };
#else
            var config = new TConfig();
#endif
            return config;
        }
    }
}