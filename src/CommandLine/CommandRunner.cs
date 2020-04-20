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
            var accessKeyToUse = accessKey.HasValue() ? accessKey.Value() : Environment.GetEnvironmentVariable(AccessKeyId);
            var secretToUse = secret.HasValue() ? secret.Value() : Environment.GetEnvironmentVariable(SecretAccessKey);
            var regionToUse = region.HasValue() ? region.Value() : Environment.GetEnvironmentVariable(Region);

            var regionEndpoint = RegionEndpoint.GetBySystemName(regionToUse);

            using(var sqs = new AmazonSQSClient(accessKeyToUse, secretToUse, regionEndpoint))
            using(var sns = new AmazonSimpleNotificationServiceClient(accessKeyToUse, secretToUse, regionEndpoint))
            using (var s3 = new AmazonS3Client(accessKeyToUse, secretToUse, regionEndpoint))
            {
                await func(sqs, sns, s3);
            }
        }

        public const string AccessKeyId = "AWS_ACCESS_KEY_ID";
        public const string Region = "AWS_REGION";
        public const string SecretAccessKey = "AWS_SECRET_ACCESS_KEY";
    }
}