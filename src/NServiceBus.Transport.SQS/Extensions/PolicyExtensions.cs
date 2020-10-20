namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Linq;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

#pragma warning disable 618
    static class PolicyExtensions
    {
        internal static bool HasSQSPermission(this Policy policy, Statement addStatement)
        {
            foreach (var statement in policy.Statements)
            {
                // See if the statement contains the topic as a resource
                var containsResource = false;
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var resource in statement.Resources)
                {
                    if (resource.Id.Equals(addStatement.Resources[0].Id))
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
                        condition.Values.Contains(addStatement.Conditions[0].Values[0]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static void AddSQSPermission(this Policy policy, Statement addStatement)
        {
            policy.Statements.Add(addStatement);
        }

        internal static Statement CreateSQSPermissionStatement(string topicArn, string sqsQueueArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            return statement;
        }
    }
#pragma warning restore 618
}