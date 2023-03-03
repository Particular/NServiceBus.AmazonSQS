namespace NServiceBus
{
    using System;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;

    static class DefaultClientFactories
    {
        public static Func<IAmazonSQS> SqsFactory = () => new AmazonSQSClient(Create<AmazonSQSConfig>());

        public static Func<IAmazonSimpleNotificationService> SnsFactory = () =>
            new AmazonSimpleNotificationServiceClient(Create<AmazonSimpleNotificationServiceConfig>());

        public static Func<IAmazonS3> S3Factory = () => new AmazonS3Client(Create<AmazonS3Config>());

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