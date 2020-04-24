namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;

    static class Endpoint
    {
        public static async Task Create(IAmazonSQS sqs, string prefix, string endpointName, double retentionPeriodInSeconds)
        {
            await Console.Out.WriteLineAsync($"Creating endpoint '{endpointName}'.");

            await Queue.Create(sqs, prefix, endpointName, retentionPeriodInSeconds);

            await Console.Out.WriteLineAsync($"Endpoint '{endpointName}' is ready.");
        }

        public static async Task Delete(IAmazonSQS sqs, string prefix, string endpointName)
        {
            await Console.Out.WriteLineAsync($"Deleting endpoint '{endpointName}'.");

            await Queue.Delete(sqs, prefix, endpointName);

            await Console.Out.WriteLineAsync($"Endpoint '{endpointName}' is deleted.");
        }

        public static async Task AddLargeMessageSupport(IAmazonS3 s3, string endpointName, string bucketName, string keyPrefix, int expirationInDays)
        {
            await Console.Out.WriteLineAsync($"Adding large message support to Endpoint '{endpointName}'.");

            await Bucket.Create(s3, endpointName, bucketName);
            await Bucket.EnableCleanup(s3, endpointName, bucketName, keyPrefix, expirationInDays);

            await Console.Out.WriteLineAsync($"Added large message support to Endpoint '{endpointName}'.");
        }

        public static async Task RemoveLargeMessageSupport(IAmazonS3 s3, string endpointName, string bucketName, bool removeSharedResources)
        {
            await Console.Out.WriteLineAsync($"Removing large message support from Endpoint '{endpointName}'.");

            if (removeSharedResources)
            {
                await Bucket.Delete(s3, endpointName, bucketName);
            }

            await Console.Out.WriteLineAsync($"Removing large message support from Endpoint '{endpointName}'.");
        }

        public static async Task AddDelayDelivery(IAmazonSQS sqs, string prefix, string endpointName, double delayInSeconds, double retentionPeriodInSeconds, string suffix)
        {
            await Console.Out.WriteLineAsync($"Adding delay delivery support to Endpoint '{endpointName}'.");

            await Queue.CreateDelayDelivery(sqs, prefix, endpointName, delayInSeconds, retentionPeriodInSeconds, suffix);

            await Console.Out.WriteLineAsync($"Added delay delivery support to Endpoint '{endpointName}'.");
        }

        public static async Task RemoveDelayDelivery(IAmazonSQS sqs, string prefix, string endpointName, string suffix)
        {
            await Console.Out.WriteLineAsync($"Removing delay delivery support from Endpoint '{endpointName}'.");

            await Queue.DeleteDelayDelivery(sqs, prefix, endpointName, suffix);

            await Console.Out.WriteLineAsync($"Removing delay delivery support from Endpoint '{endpointName}'.");
        }

        public static async Task Subscribe(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, string prefix, string endpointName, string eventType)
        {
            await Console.Out.WriteLineAsync($"Subscribing endpoint '{endpointName}' to '{eventType}'.");

            var queueUrl = await Queue.GetUrl(sqs, prefix, endpointName);
            var topicArn = await Topic.Create(sns, prefix, eventType);
            await Topic.Subscribe(sqs, sns, topicArn, queueUrl);

            await Console.Out.WriteLineAsync($"Endpoint '{endpointName}' subscribed to '{eventType}'.");
        }

        public static async Task Unsubscribe(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, string prefix, string endpointName, string eventType, bool removeSharedResources)
        {
            await Console.Out.WriteLineAsync($"Unsubscribing endpoint '{endpointName}' from '{eventType}'.");

            var queueArn = await Queue.GetArn(sqs, prefix, endpointName);
            var topicArn = await Topic.Get(sns, prefix, eventType);
            await Topic.Unsubscribe(sns, topicArn, queueArn);

            await Console.Out.WriteLineAsync($"Endpoint '{endpointName}' unsubscribed from '{eventType}'.");

            if (removeSharedResources)
            {
                await Topic.Delete(sns, topicArn);
            }
        }
    }
}