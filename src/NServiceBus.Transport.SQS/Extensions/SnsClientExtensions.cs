namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Unicast.Messages;

    static class SnsClientExtensions
    {
        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, MessageMetadata metadata, string queueName, SnsListSubscriptionsByTopicRateLimiter snsListSubscriptionsByTopicRateLimiter)
        {
            var topic = await topicCache.GetTopic(metadata).ConfigureAwait(false);
            return await snsClient.FindMatchingSubscription(queueCache, topic, queueName, snsListSubscriptionsByTopicRateLimiter).ConfigureAwait(false);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, string topicName, string queueName, SnsListTopicsRateLimiter snsListTopicsRateLimiter, SnsListSubscriptionsByTopicRateLimiter snsListSubscriptionsByTopicRateLimiter)
        {
            var existingTopic = await snsListTopicsRateLimiter.Execute(async () =>
            {
                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }).ConfigureAwait(false);

            if (existingTopic == null)
            {
                return null;
            }

            return await snsClient.FindMatchingSubscription(queueCache, existingTopic, queueName, snsListSubscriptionsByTopicRateLimiter)
                .ConfigureAwait(false);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, Topic topic, string queueName, SnsListSubscriptionsByTopicRateLimiter snsListSubscriptionsByTopicRateLimiter = null)
        {
            var physicalQueueName = queueCache.GetPhysicalQueueName(queueName);

            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;

            do
            {
                if (snsListSubscriptionsByTopicRateLimiter != null)
                {
                    upToAHundredSubscriptions = await snsListSubscriptionsByTopicRateLimiter.Execute(async () =>
                    {
                        return await snsClient.ListSubscriptionsByTopicAsync(topic.TopicArn, upToAHundredSubscriptions?.NextToken)
                            .ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
                else
                {
                    upToAHundredSubscriptions = await snsClient.ListSubscriptionsByTopicAsync(topic.TopicArn, upToAHundredSubscriptions?.NextToken)
                            .ConfigureAwait(false);
                }

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