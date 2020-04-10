namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Unicast.Messages;

    static class SnsClientExtensions
    {
        public static Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, MessageMetadata metadata, string queueName)
        {
            var topicName = topicCache.GetTopicName(metadata);
            return snsClient.FindMatchingSubscription(queueCache, topicName, queueName);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, string topicName, string queueName)
        {
            var existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            if (existingTopic == null)
            {
                return null;
            }

            return await snsClient.FindMatchingSubscription(queueCache, existingTopic, queueName)
                .ConfigureAwait(false);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, Topic topic, string queueName)
        {
            var physicalQueueName = queueCache.GetPhysicalQueueName(queueName);

            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;

            do
            {
                upToAHundredSubscriptions = await snsClient.ListSubscriptionsByTopicAsync(topic.TopicArn, upToAHundredSubscriptions?.NextToken)
                    .ConfigureAwait(false);

                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
                {
                    if (upToAHundredSubscription.Endpoint.EndsWith($":{physicalQueueName}", StringComparison.Ordinal))
                    {
                        return upToAHundredSubscription.SubscriptionArn;
                    }
                }
            } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            return null;
        }
    }
}