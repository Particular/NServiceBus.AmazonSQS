namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Linq;

    static class PolicyExtensions
    {
        internal static bool HasSQSPermission(this Policy policy, Statement addStatement)
        {
            foreach (var statement in policy.Statements)
            {
                // See if the statement contains the topic as a resource
                var containsResource = false;
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

                foreach (var condition in statement.Conditions)
                {
                    if ((string.Equals(condition.Type, "StringLike", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(condition.Type, "StringEquals", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(condition.Type, "ArnEquals", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(condition.Type, "ArnLike", StringComparison.OrdinalIgnoreCase)) &&
                        string.Equals(condition.ConditionKey, "aws:SourceArn", StringComparison.OrdinalIgnoreCase) &&
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
            statement.Actions.Add(new ActionIdentifier { ActionName = "sqs:SendMessage" });
            statement.Resources.Add(new Resource { Id = sqsQueueArn });
            statement.Conditions.Add(new Condition { Type = "ArnLike", ConditionKey = "aws:SourceArn", Values = new[] { topicArn }});
            statement.Principals.Add(new Principal{ Provider = "AWS", Id = "*" });
            return statement;
        }
    }
}