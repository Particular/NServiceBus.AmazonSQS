namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Linq;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

    static class PolicyExtensions
    {
        internal static bool HasSQSPermission(this Policy policy, string topicArn, string sqsQueueArn)
        {
            foreach (var statement in policy.Statements)
            {
                // See if the statement contains the topic as a resource
                var containsResource = false;
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var resource in statement.Resources)
                {
                    if (resource.Id.Equals(sqsQueueArn))
                    {
                        containsResource = true;
                        break;
                    }
                }

                // If queue found as the resource see if the condition is for this topic
                if (!containsResource)
                {
                    continue;
                }

                // ReSharper disable once LoopCanBeConvertedToQuery
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

            return false;
        }

        internal static void AddSQSPermission(this Policy policy, string topicArn, string sqsQueueArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            policy.Statements.Add(statement);
        }
    }
}