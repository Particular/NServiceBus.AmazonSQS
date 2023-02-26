namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using McMaster.Extensions.CommandLineUtils;

    static class CommandRunner
    {
        public static async Task Run(CommandOption accessKey, CommandOption secret, CommandOption region, Func<IAmazonSQS, IAmazonSimpleNotificationService, IAmazonS3, Task> func)
        {
            var useCredentialsFromOptions = accessKey.HasValue() && secret.HasValue();
            var useRegionFromOptions = region.HasValue();

            var useFullConstructor = useCredentialsFromOptions && useRegionFromOptions;

            using var sqs = useFullConstructor ? new AmazonSQSClient(accessKey.Value(), secret.Value(), Create<AmazonSQSConfig>(RegionEndpoint.GetBySystemName(region.Value()))) :
                useCredentialsFromOptions ? new AmazonSQSClient(accessKey.Value(), secret.Value(), Create<AmazonSQSConfig>()) :
                useRegionFromOptions ? new AmazonSQSClient(Create<AmazonSQSConfig>(RegionEndpoint.GetBySystemName(region.Value()))) :
                new AmazonSQSClient(Create<AmazonSQSConfig>());
            using var sns = useFullConstructor ? new AmazonSimpleNotificationServiceClient(accessKey.Value(), secret.Value(), Create<AmazonSimpleNotificationServiceConfig>(RegionEndpoint.GetBySystemName(region.Value()))) :
                useCredentialsFromOptions ? new AmazonSimpleNotificationServiceClient(accessKey.Value(), secret.Value(), Create<AmazonSimpleNotificationServiceConfig>()) :
                useRegionFromOptions ? new AmazonSimpleNotificationServiceClient(Create<AmazonSimpleNotificationServiceConfig>(RegionEndpoint.GetBySystemName(region.Value()))) :
                new AmazonSimpleNotificationServiceClient(Create<AmazonSimpleNotificationServiceConfig>());
            using var s3 = useFullConstructor ? new AmazonS3Client(accessKey.Value(), secret.Value(), Create<AmazonS3Config>(RegionEndpoint.GetBySystemName(region.Value()))) :
                useCredentialsFromOptions ? new AmazonS3Client(accessKey.Value(), secret.Value(), Create<AmazonS3Config>()) :
                useRegionFromOptions ? new AmazonS3Client(Create<AmazonS3Config>(RegionEndpoint.GetBySystemName(region.Value()))) :
                new AmazonS3Client(Create<AmazonS3Config>());
            await func(sqs, sns, s3);
        }

        // This helper can also be removed alongside with the other create method. There are various constructor
        // overloads available that directly accept the region endpoint.
        static TConfig Create<TConfig>(RegionEndpoint regionEndpoint) where TConfig : ClientConfig, new()
            => Create<TConfig>(cfg => cfg.RegionEndpoint = regionEndpoint);

        // Can be removed once https://github.com/aws/aws-sdk-net/issues/1929 is addressed by the team
        // setting the cache size to 1 will significantly improve the throughput on non-windows OSS while
        // windows had already 1 as the default.
        // There might be other occurrences of setting this setting explicitly in the code base. Make sure to remove them
        // consistently once the issue is addressed. 
        static TConfig Create<TConfig>(Action<TConfig> configure = null)
            where TConfig : ClientConfig, new()
        {
#if NET
            var config = new TConfig { HttpClientCacheSize = 1 };
#else
            var config = new TConfig();
#endif
            configure?.Invoke(config);
            return config;
        }

        public const string AccessKeyId = "AWS_ACCESS_KEY_ID";
        public const string Region = "AWS_REGION";
        public const string SecretAccessKey = "AWS_SECRET_ACCESS_KEY";
    }
}