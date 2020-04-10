namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
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

        internal static void AddSQSPermission(Policy policy, string topicArn, string sqsQueueArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            policy.Statements.Add(statement);
        }

        internal static bool HasSQSPermission(Policy policy, string topicArn, string sqsQueueArn)
        {
            foreach (var statement in policy.Statements)
            {
                // See if the statement contains the topic as a resource
                var containsResource = false;
                foreach (var resource in statement.Resources)
                {
                    if (resource.Id.Equals(sqsQueueArn))
                    {
                        containsResource = true;
                        break;
                    }
                }

                // If queue found as the resource see if the condition is for this topic
                if (containsResource)
                {
                    foreach (var condition in statement.Conditions)
                    {
                        if ((string.Equals(condition.Type, ConditionFactory.StringComparisonType.StringLike.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(condition.Type, ConditionFactory.StringComparisonType.StringEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(condition.Type, ConditionFactory.ArnComparisonType.ArnEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(condition.Type, ConditionFactory.ArnComparisonType.ArnLike.ToString(), StringComparison.OrdinalIgnoreCase)) &&
                            string.Equals(condition.ConditionKey, ConditionFactory.SOURCE_ARN_CONDITION_KEY, StringComparison.OrdinalIgnoreCase) &&
                            condition.Values.Contains(topicArn))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}