namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;

    static class SnsClientExtensions
    {
        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, TransportConfiguration configuration, string topicName, string queueName)
        {
            var existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            if (existingTopic == null)
            {
                return null;
            }

            return await snsClient.FindMatchingSubscription(configuration, existingTopic, queueName)
                .ConfigureAwait(false);
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService snsClient, TransportConfiguration configuration, Topic topic, string queueName)
        {
            var sqsQueueName = QueueNameHelper.GetSqsQueueName(queueName, configuration);

            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;

            do
            {
                upToAHundredSubscriptions = await snsClient.ListSubscriptionsByTopicAsync(topic.TopicArn, upToAHundredSubscriptions?.NextToken)
                    .ConfigureAwait(false);

                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
                {
                    if (upToAHundredSubscription.Endpoint.EndsWith($":{sqsQueueName}", StringComparison.Ordinal))
                    {
                        return upToAHundredSubscription.SubscriptionArn;
                    }
                }
            } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            return null;
        }
    }
}