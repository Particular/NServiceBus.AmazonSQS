namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;

    static class Topic
    {
        public static async Task<string> Get(IAmazonSimpleNotificationService sns, string prefix, string eventType)
        {
            var topicName = $"{prefix}{eventType}";
            var sanitized = TopicSanitization.GetSanitizedTopicName(topicName);
            var findTopicResponse = await sns.FindTopicAsync(sanitized).ConfigureAwait(false);
            return findTopicResponse?.TopicArn;
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

        public static async Task<string> Subscribe(IAmazonSimpleNotificationService sns, string topicArn, string queueArn)
        {
            await Console.Out.WriteLineAsync($"Subscribing queue with ARN '{queueArn}' to topic with ARN '{topicArn}'.");

            var createdSubscription = await sns.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = topicArn,
                Protocol = "sqs",
                Endpoint = queueArn,
                ReturnSubscriptionArn = true,
                Attributes = new Dictionary<string, string>
                {
                    { "RawMessageDelivery", "true" }
                }
            }).ConfigureAwait(false);

            await Console.Out.WriteLineAsync($"Queue with ARN '{queueArn}' subscribed to topic with ARN '{topicArn}'.");

            return createdSubscription.SubscriptionArn;
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