namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using McMaster.Extensions.CommandLineUtils;

    static class Endpoint
    {
        public static async Task Create(IAmazonSQS sqs, CommandArgument name)
        {
            await Console.Out.WriteLineAsync($"Creating endpoint '{name.Value}'.");

            await Queue.Create(sqs, name);

            await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' is ready.");
        }

        public static async Task Delete(IAmazonSQS sqs, CommandArgument name)
        {
            await Console.Out.WriteLineAsync($"Deleting endpoint '{name.Value}'.");

            await Queue.Delete(sqs, name);

            await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' is deleted.");
        }

        public static async Task AddLargeMessageSupport(IAmazonS3 s3, CommandArgument name, CommandArgument bucketName)
        {
            await Console.Out.WriteLineAsync($"Adding large message support to Endpoint '{name.Value}'.");

            await Bucket.Create(s3, name, bucketName);
            await Bucket.EnableCleanup(s3, name, bucketName);

            await Console.Out.WriteLineAsync($"Added large message support to Endpoint '{name.Value}'.");
        }

        public static async Task RemoveLargeMessageSupport(IAmazonS3 s3, CommandArgument name, CommandArgument bucketName)
        {
            await Console.Out.WriteLineAsync($"Removing large message support from Endpoint '{name.Value}'.");

            await Bucket.Delete(s3, name, bucketName);

            await Console.Out.WriteLineAsync($"Removing large message support from Endpoint '{name.Value}'.");
        }

        public static async Task AddDelayDelivery(IAmazonSQS sqs, CommandArgument name)
        {
            await Console.Out.WriteLineAsync($"Adding delay delivery support to Endpoint '{name.Value}'.");

            await Queue.CreateDelayDelivery(sqs, name);

            await Console.Out.WriteLineAsync($"Added delay delivery support to Endpoint '{name.Value}'.");
        }

        public static async Task RemoveDelayDelivery(IAmazonSQS sqs, CommandArgument name)
        {
            await Console.Out.WriteLineAsync($"Removing delay delivery support from Endpoint '{name.Value}'.");

            await Queue.DeleteDelayDelivery(sqs, name);

            await Console.Out.WriteLineAsync($"Removing delay delivery support from Endpoint '{name.Value}'.");
        }

        public static async Task Subscribe(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, CommandArgument name, CommandArgument eventType)
        {
            await Console.Out.WriteLineAsync($"Subscribing endpoint '{name.Value}' to '{eventType.Value}'.");

            var queueUrl = await Queue.GetUrl(sqs, name);
            var topicArn = await Topic.Create(sns, eventType);
            var subscriptionArn = await Topic.Subscribe(sqs, sns, topicArn, queueUrl);

            await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' subscribed to '{eventType.Value}'.");
        }

        public static async Task Unsubscribe(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, CommandArgument name, CommandArgument eventType)
        {
            await Console.Out.WriteLineAsync($"Unsubscribing endpoint '{name.Value}' from '{eventType.Value}'.");

            var queueArn = await Queue.GetArn(sqs, name);
            var topicArn = await Topic.Get(sns, eventType);
            await Topic.Unsubscribe(sns, topicArn, queueArn);

            await Console.Out.WriteLineAsync($"Endpoint '{name.Value}' unsubscribed from '{eventType.Value}'.");
        }


    }

}