namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

#pragma warning disable 618
    static class PolicyExtensions
    {
        internal static void AddSQSPermission(this Policy policy, Statement addStatement)
        {
            policy.Statements.Add(addStatement);
        }

        internal static Statement CreateSQSPermissionStatement(string sqsQueueArn, string topicArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            return statement;
        }

        internal static Statement CreateSQSPermissionStatement(string sqsQueueArn, IEnumerable<string> topicArns)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Principals.Add(new Principal("*"));
            var queuePermissionCondition = new Condition(ConditionFactory.ArnComparisonType.ArnLike.ToString(), "aws:SourceArn", topicArns.ToArray());
            statement.Conditions.Add(queuePermissionCondition);
            return statement;
        }

        internal static Statement CreateSQSPermissionStatement(string sqsQueueArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Principals.Add(new Principal("*"));
            return statement;
        }

        // Checks whether exactly one condition is present that matches the add statement condition
        public static bool ExactlyOneConditionContainedInStatement(Condition condition, Statement addStatement)
        {
            return (string.Equals(condition.Type, ConditionFactory.StringComparisonType.StringLike.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(condition.Type, ConditionFactory.StringComparisonType.StringEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(condition.Type, ConditionFactory.ArnComparisonType.ArnEquals.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(condition.Type, ConditionFactory.ArnComparisonType.ArnLike.ToString(), StringComparison.OrdinalIgnoreCase)) &&
                   string.Equals(condition.ConditionKey, ConditionFactory.SOURCE_ARN_CONDITION_KEY, StringComparison.OrdinalIgnoreCase) &&
                   condition.Values.Contains(addStatement.Conditions[0].Values[0]) &&
                   condition.Values.Length == 1;
        }
    }
#pragma warning restore 618
}