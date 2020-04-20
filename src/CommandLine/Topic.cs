namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;

    static class Topic
    {
        public static async Task<string> Get(IAmazonSimpleNotificationService sns, string prefix, string eventType)
        {
            var topicName = $"{prefix}{eventType}";
            var sanitized = TopicSanitization.GetSanitizedTopicName(topicName);
            var findTopicResponse = await sns.FindTopicAsync(sanitized).ConfigureAwait(false);
            return findTopicResponse.TopicArn;
        }

        public static async Task<string> Create(IAmazonSimpleNotificationService sns, string prefix, string eventType)
        {
            var topicName = $"{prefix}{eventType}";
            var sanitized = TopicSanitization.GetSanitizedTopicName(topicName);
            await Console.Out.WriteLineAsync($"Creating SNS Topic with name '{sanitized}'.");
            var createTopicResponse = await sns.CreateTopicAsync(sanitized).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SNS Topic with name '{sanitized}'.");
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

        public static async Task Unsubscribe(IAmazonSimpleNotificationService sns, string topicArn, string queueArn)
        {
            await Console.Out.WriteLineAsync($"Unsubscribing queue with ARN '{queueArn}' from topic with ARN '{topicArn}'.");

            var subscriptionArn = await sns.FindMatchingSubscription(topicArn, queueArn);

            await sns.UnsubscribeAsync(subscriptionArn).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Queue with ARN '{queueArn}' unsubscribed from topic with ARN '{topicArn}'.");
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService sns, string topicArn, string queueArn)
        {
            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;

            do
            {
                upToAHundredSubscriptions = await sns.ListSubscriptionsByTopicAsync(topicArn, upToAHundredSubscriptions?.NextToken)
                    .ConfigureAwait(false);

                foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
                {
                    if (upToAHundredSubscription.Endpoint == queueArn)
                    {
                        return upToAHundredSubscription.SubscriptionArn;
                    }
                }
            } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            return null;
        }

        public static async Task Delete(IAmazonSimpleNotificationService sns, string topicArn)
        {
            await Console.Out.WriteLineAsync($"Deleting topic with ARN '{topicArn}'.");

            await sns.DeleteTopicAsync(topicArn).ConfigureAwait(false);

            await Console.Out.WriteLineAsync($"Deleted topic with ARN '{topicArn}'.");
        }
    }

}