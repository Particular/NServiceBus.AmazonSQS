namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon;
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

            using var sqs = useFullConstructor ? new AmazonSQSClient(accessKey.Value(), secret.Value(), new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region.Value()) }) :
                useCredentialsFromOptions ? new AmazonSQSClient(accessKey.Value(), secret.Value(), new AmazonSQSConfig()) :
                useRegionFromOptions ? new AmazonSQSClient(new AmazonSQSConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region.Value()) }) :
                new AmazonSQSClient(new AmazonSQSConfig());
            using var sns = useFullConstructor ? new AmazonSimpleNotificationServiceClient(accessKey.Value(), secret.Value(), new AmazonSimpleNotificationServiceConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region.Value()) }) :
                useCredentialsFromOptions ? new AmazonSimpleNotificationServiceClient(accessKey.Value(), secret.Value(), new AmazonSimpleNotificationServiceConfig()) :
                useRegionFromOptions ? new AmazonSimpleNotificationServiceClient(new AmazonSimpleNotificationServiceConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region.Value()) }) :
                new AmazonSimpleNotificationServiceClient(new AmazonSimpleNotificationServiceConfig());
            using var s3 = useFullConstructor ? new AmazonS3Client(accessKey.Value(), secret.Value(), new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(region.Value()) }) :
                useCredentialsFromOptions ? new AmazonS3Client(accessKey.Value(), secret.Value(), new AmazonS3Config()) :
                useRegionFromOptions ? new AmazonS3Client(new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(region.Value()) }) :
                new AmazonS3Client(new AmazonS3Config());
            await func(sqs, sns, s3);
        }

        public const string AccessKeyId = "AWS_ACCESS_KEY_ID";
        public const string Region = "AWS_REGION";
        public const string SecretAccessKey = "AWS_SECRET_ACCESS_KEY";
    }
}