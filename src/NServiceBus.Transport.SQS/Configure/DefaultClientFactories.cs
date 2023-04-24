#nullable enable

namespace NServiceBus
{
    using System;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;

    static class DefaultClientFactories
    {
        public static Func<IAmazonSQS> SqsFactory = () => new AmazonSQSClient(new AmazonSQSConfig());

        public static Func<IAmazonSimpleNotificationService> SnsFactory = () =>
            new AmazonSimpleNotificationServiceClient(new AmazonSimpleNotificationServiceConfig());

        public static Func<IAmazonS3> S3Factory = () => new AmazonS3Client(new AmazonS3Config());
    }
}