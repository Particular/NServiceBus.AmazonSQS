namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using McMaster.Extensions.CommandLineUtils;

    static class Topic
    {
        public static async Task<string> Create(IAmazonSimpleNotificationService sns, CommandArgument eventType)
        {
            var topicName = GetSanitizedTopicName(eventType.Value);
            await Console.Out.WriteLineAsync($"Creating SNS Topic with name '{topicName}'.");
            var createTopicResponse = await sns.CreateTopicAsync(topicName).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SNS Topic with name '{topicName}'.");
            return createTopicResponse.TopicArn;
        }

        public static async Task<string> Subscribe(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, string topicArn, string queueUrl)
        {
            await Console.Out.WriteLineAsync($"Subscribing queue with url '{queueUrl}' to topic with ARN '{topicArn}'.");
            var createdSubscription = await sns.SubscribeQueueAsync(topicArn, sqs, queueUrl).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Queue with url '{queueUrl}' subscribed to topic with ARN '{topicArn}'.");
            await Console.Out.WriteLineAsync($"Setting raw delivery for subscription with arn '{createdSubscription}'.");
            await sns.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest
            {
                SubscriptionArn = createdSubscription,
                AttributeName = "RawMessageDelivery",
                AttributeValue = "true"
            }).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Raw delivery for subscription with arn '{createdSubscription}' set.");
            return createdSubscription;
        }

        public static string GetSanitizedTopicName(string topicName)
        {
            var topicNameBuilder = new StringBuilder(topicName);
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < topicNameBuilder.Length; ++i)
            {
                var c = topicNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    topicNameBuilder[i] = '-';
                }
            }

            return topicNameBuilder.ToString();
        }
    }

}