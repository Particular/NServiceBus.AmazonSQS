namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

#pragma warning disable 618
    static class PolicyExtensions
    {
        internal static bool Update(this Policy policy, IReadOnlyCollection<PolicyStatement> addPolicyStatements,
            bool addAccountConditionForPolicies, bool addTopicNamePrefixConditionForPolicies,
            IReadOnlyList<string> namespaceConditionsForPolicies, string topicNamePrefix, string sqsQueueArn)
        {
            if (addPolicyStatements.Count == 0)
            {
                return false;
            }

            var policyModified = false;
            var wildcardConditions = new List<string>();
            if (addAccountConditionForPolicies)
            {
                wildcardConditions.AddRange(addPolicyStatements.Select(s => $"{s.AccountArn}:*").Distinct());
            }

            if (addTopicNamePrefixConditionForPolicies)
            {
                wildcardConditions.AddRange(addPolicyStatements
                    .Select(s => $"{s.AccountArn}:{topicNamePrefix}*").Distinct());
            }

            if (namespaceConditionsForPolicies.Count > 0)
            {
                wildcardConditions.AddRange(namespaceConditionsForPolicies
                    .SelectMany(ns => addPolicyStatements
                        .Select(s => $"{s.AccountArn}")
                        .Distinct()
                        .Select(arn => $"{arn}:{GetNamespaceName(topicNamePrefix, ns)}*")));
            }

            var wildCardQueuePermissionStatements = CreatePermissionStatementForQueueMatching(sqsQueueArn, wildcardConditions);
            var explicitQueuePermissionStatements =
                CreatePermissionStatementForQueueMatching(sqsQueueArn, addPolicyStatements.Select(s => s.TopicArn));

            if (wildcardConditions.Count > 0 || !policy.ContainsPermission(explicitQueuePermissionStatements))
            {
                var statementToRemoves = policy.Statements
                    .Where(statement => statement.CoveredByPermission(explicitQueuePermissionStatements)).ToList();
                foreach (var statementToRemove in statementToRemoves)
                {
                    policy.Statements.Remove(statementToRemove);
                    policyModified = true;
                }

                if (wildcardConditions.Count == 0)
                {
                    policy.Statements.Add(explicitQueuePermissionStatements);

                    var wildCardStatementsToRemove = policy.Statements
                        .Where(statement => statement.CoveredByWildcard(explicitQueuePermissionStatements)).ToList();
                    foreach (var statementToRemove in wildCardStatementsToRemove)
                    {
                        policy.Statements.Remove(statementToRemove);
                    }

                    policyModified = true;
                }
            }

            if (wildcardConditions.Count > 0 && !policy.ContainsPermission(wildCardQueuePermissionStatements))
            {
                var statementToRemoves = policy.Statements
                    .Where(statement => statement.CoveredByWildcard(wildCardQueuePermissionStatements)).ToList();
                foreach (var statementToRemove in statementToRemoves)
                {
                    policy.Statements.Remove(statementToRemove);
                }

                policy.Statements.Add(wildCardQueuePermissionStatements);

                policyModified = true;
            }

            return policyModified;
        }

        internal static Policy ExtractPolicy(this Dictionary<string, string> queueAttributes)
        {
            string policyStr = null;
            if (queueAttributes.ContainsKey("Policy"))
            {
                policyStr = queueAttributes["Policy"];
            }

            return string.IsNullOrEmpty(policyStr) ? new Policy() : Policy.FromJson(policyStr);
        }

        private static string GetNamespaceName(string topicNamePrefix, string namespaceName)
        {
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            var namespaceNameBuilder = new StringBuilder(namespaceName);
            for (var i = 0; i < namespaceNameBuilder.Length; ++i)
            {
                var c = namespaceNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    namespaceNameBuilder[i] = '-';
                }
            }

            // topicNamePrefix should not be sanitized
            return topicNamePrefix + namespaceNameBuilder;
        }

        private static bool ContainsPermission(this Policy policy, Statement statement)
        {
            if (policy.Statements == null)
            {
                return false;
            }

            return policy.Statements.Any(stmt => stmt.Effect == statement.Effect &&
                                                       stmt.StatementContainsResources(statement.Resources) &&
                                                       stmt.StatementContainsActions(statement.Actions) &&
                                                       stmt.StatementContainsConditions(statement.Conditions) &&
                                                       stmt.StatementContainsPrincipals(statement.Principals));
        }

        private static bool CoveredByPermission(this Statement statement, Statement permission) =>
            statement.Effect == permission.Effect &&
            statement.StatementContainsResources(permission.Resources) &&
            statement.StatementContainsActions(permission.Actions) &&

            statement.StatementCoveredByConditions(permission.Conditions) &&

            statement.StatementContainsPrincipals(permission.Principals);

        private static bool CoveredByWildcard(this Statement statement, Statement permission) =>
            statement.Effect == permission.Effect &&
            statement.StatementContainsResources(permission.Resources) &&
            statement.StatementContainsActions(permission.Actions) &&

            statement.StatementCoveredByWildcardConditions() &&

            statement.StatementContainsPrincipals(permission.Principals);

        private static bool StatementContainsResources(this Statement statement, IEnumerable<Resource> resources) =>
            resources.All(resource => statement.Resources.FirstOrDefault(x => string.Equals(x.Id, resource.Id)) != null);

        private static bool StatementContainsActions(
            this Statement statement,
            IEnumerable<ActionIdentifier> actions) =>
            actions.All(action => statement.Actions.FirstOrDefault(x => string.Equals(x.ActionName, action.ActionName)) != null);

        private static bool StatementContainsPrincipals(
            this Statement statement,
            IEnumerable<Principal> principals) =>
            principals.All(principal => statement.Principals.FirstOrDefault(x => string.Equals(x.Id, principal.Id) && string.Equals(x.Provider, principal.Provider)) != null);

        private static bool StatementContainsConditions(
            this Statement statement,
            IEnumerable<Condition> conditions) =>
            conditions.All(condition => statement.Conditions.FirstOrDefault(x => string.Equals(x.Type, condition.Type) &&
                string.Equals(x.ConditionKey, condition.ConditionKey) &&
                x.Values.OrderBy(v => v, OrdinalComparer).SequenceEqual(condition.Values.OrderBy(v => v, OrdinalComparer), OrdinalComparer)) != null);

        private static bool StatementCoveredByConditions(
            this Statement statement,
            IList<Condition> conditions) =>
            statement.Conditions.Any(condition => conditions.Any(x => string.Equals(x.Type, condition.Type) && string.Equals(x.ConditionKey, condition.ConditionKey) && condition.Values.All(v => x.Values.Contains(v, OrdinalComparer))));

        private static bool StatementCoveredByWildcardConditions(
            this Statement statement) =>
            statement.Conditions.Any(condition => condition.Values.All(v => v.Contains("*")));

        internal static Statement CreatePermissionStatementForQueueMatching(string queueArn, IEnumerable<string> topicArnMatchPatterns)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(queueArn));
            statement.Principals.Add(new Principal("*"));
            var queuePermissionCondition = new Condition(ConditionFactory.ArnComparisonType.ArnLike.ToString(), "aws:SourceArn", topicArnMatchPatterns.OrderBy(t => t, OrdinalComparer).ToArray());
            statement.Conditions.Add(queuePermissionCondition);
            return statement;
        }

        // conditions are case sensitive
        // https://stackoverflow.com/a/47769284/290290
        private static readonly StringComparer OrdinalComparer = StringComparer.Ordinal;
    }
#pragma warning restore 618
}