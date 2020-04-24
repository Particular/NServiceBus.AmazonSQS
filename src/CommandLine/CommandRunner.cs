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

            using (var sqs = useFullConstructor ? new AmazonSQSClient(accessKey.Value(), secret.Value(), RegionEndpoint.GetBySystemName(region.Value())) :
                useCredentialsFromOptions ? new AmazonSQSClient(accessKey.Value(), secret.Value()) :
                useRegionFromOptions ? new AmazonSQSClient(RegionEndpoint.GetBySystemName(region.Value())) :
                new AmazonSQSClient())
            using (var sns = useFullConstructor ? new AmazonSimpleNotificationServiceClient(accessKey.Value(), secret.Value(), RegionEndpoint.GetBySystemName(region.Value())) :
                useCredentialsFromOptions ? new AmazonSimpleNotificationServiceClient(accessKey.Value(), secret.Value()) :
                useRegionFromOptions ? new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(region.Value())) :
                new AmazonSimpleNotificationServiceClient())
            using (var s3 = useFullConstructor ? new AmazonS3Client(accessKey.Value(), secret.Value(), RegionEndpoint.GetBySystemName(region.Value())) :
                useCredentialsFromOptions ? new AmazonS3Client(accessKey.Value(), secret.Value()) :
                useRegionFromOptions ? new AmazonS3Client(RegionEndpoint.GetBySystemName(region.Value())) :
                new AmazonS3Client())
            {
                await func(sqs, sns, s3);
            }
        }


        public const string AccessKeyId = "AWS_ACCESS_KEY_ID";
        public const string Region = "AWS_REGION";
        public const string SecretAccessKey = "AWS_SECRET_ACCESS_KEY";
    }
}