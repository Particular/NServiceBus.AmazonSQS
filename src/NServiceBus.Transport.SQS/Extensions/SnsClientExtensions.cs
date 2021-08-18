namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Unicast.Messages;

    static class SnsClientExtensions
    {
        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, MessageMetadata metadata, string queueName)
        {
            var topic = await topicCache.GetTopic(metadata).ConfigureAwait(false);
            return await snsClient.FindMatchingSubscription(queueCache, topic, queueName).ConfigureAwait(false);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, QueueCache queueCache, string topicName, string queueName, RateLimiter snsListTopicsRateLimiter)
        {
            var existingTopic = await snsListTopicsRateLimiter.Execute(async () =>
            {
                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }).ConfigureAwait(false);

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
                //ListSubscriptionsByTopic comes with a rate limit of 30 requests/second.
                //Each request can return up to 100 subscriptions. To exceed the limit a
                //publisher, in hybrid mode, should be trying to publish the same event to
                //3000+1 subscribers, or 10 different events should be published in a tight
                //loop to 300+1 subscribers, and so on.
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