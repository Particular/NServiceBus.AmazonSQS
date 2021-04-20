namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;

    static class SnsClientExtensions
    {
        public static Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, Type eventType, string queueName, CancellationToken cancellationToken = default)
        {
            var topicName = topicCache.GetTopicName(eventType);
            return snsClient.FindMatchingSubscription(queueCache, topicName, queueName, cancellationToken: cancellationToken);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, string topicName, string queueName, CancellationToken cancellationToken = default)
        {
            var existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            if (existingTopic == null)
            {
                return null;
            }

            return await snsClient.FindMatchingSubscription(queueCache, existingTopic, queueName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, Topic topic, string queueName, CancellationToken cancellationToken = default)
        {
            var physicalQueueName = queueCache.GetPhysicalQueueName(queueName);

            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;

            do
            {
                upToAHundredSubscriptions = await snsClient.ListSubscriptionsByTopicAsync(topic.TopicArn, upToAHundredSubscriptions?.NextToken, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
                {
                    if (upToAHundredSubscription.Endpoint.EndsWith($":{physicalQueueName}", StringComparison.Ordinal))
                    {
                        return upToAHundredSubscription.SubscriptionArn;
                    }
                }
            }
            while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            return null;
        }
    }
}