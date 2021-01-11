using System;
using System.Linq;
using System.Text.RegularExpressions;

#pragma warning disable 618
namespace NServiceBus.Transport.SQS.Tests
{
    using NUnit.Framework;
    using System.Collections.Generic;
    using Amazon.Auth.AccessControlPolicy;
    using Extensions;
    using Particular.Approvals;

    [TestFixture]
    public class PolicyExtensionsTests
    {
        [Test]
        public void ExtractsPolicy_if_not_empty()
        {
           var policy = new Policy {Id = "CustomPolicy"};
            var attributes = new Dictionary<string, string>
            {
                { "Policy", policy.ToJson() }
            };

            Approver.Verify(attributes.ExtractPolicy());
        }

        [Test]
        public void ExtractsPolicy_returns_empty_policy_if_empty()
        {
            var attributes = new Dictionary<string, string>();

            Approver.Verify(attributes.ExtractPolicy());
        }

        [Test]
        public void Update_with_empty_add_statements_doesnt_update_policy()
        {
            var policy = new Policy();

            var emptyAddStatements = new List<PolicyStatement>();

            var updated = policy.Update(addPolicyStatements: emptyAddStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsFalse(updated, "Policy was updated but shouldn't have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_sets_full_policy()
        {
            var policy = new Policy();

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_account_condition_sets_full_policy()
        {
            var policy = new Policy();

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: true,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_topic_name_prefix_sets_full_policy()
        {
            var policy = new Policy();

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: true,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: "DEV-",
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_namespace_conditions_sets_full_policy()
        {
            var policy = new Policy();

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>
                {
                    "Shipping.",
                    "Sales.HighValueOrders."
                },
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_namespace_conditions_and_topic_name_prefix_sets_full_policy()
        {
            var policy = new Policy();

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>
                {
                    "Shipping.",
                    "Sales.HighValueOrders."
                },
                topicNamePrefix: "DEV-",
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_all_conditions_sets_full_policy()
        {
            var policy = new Policy();

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: true,
                addTopicNamePrefixConditionForPolicies: true,
                namespaceConditionsForPolicies: new List<string>
                {
                    "Shipping.",
                    "Sales.HighValueOrders."
                },
                topicNamePrefix: "DEV-",
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_all_conditions_sets_full_policy_with_replaced_wildcard()
        {
            var policy = new Policy();
            var permissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[]
            {
                "arn:aws:sns:us-west-2:123456789012:DEV-*",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-*"
            });
            policy.Statements.Add(permissionStatement);

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: true,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: "DEV-",
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_policy_does_extend_policy()
        {
            var policy = new Policy();
            var sqsPermissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[] { "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event" });
            policy.Statements.Add(sqsPermissionStatement);

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_policy_switched_to_wildcard()
        {
            var policy = new Policy();
            var permissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[]
            {
                "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"
            });
            policy.Statements.Add(permissionStatement);

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: true,
                addTopicNamePrefixConditionForPolicies: true,
                namespaceConditionsForPolicies: new List<string>
                {
                    "NServiceBus.Transport.SQS.Tests.SubscriptionManagerTests."
                },
                topicNamePrefix: "DEV-",
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_wildcard_policy_switched_to_events_will_replace_wildcards()
        {
            var policy = new Policy();
            var permissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[]
            {
                "arn:aws:sns:us-west-2:123456789012:*",
                "arn:aws:sns:us-west-2:123456789012:DEV-*",
                "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-*",
            });
            policy.Statements.Add(permissionStatement);

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_nothing_to_subscribe_doesnt_override_existing_policy()
        {
            var policy = new Policy();
            var permissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[]
            {
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"
            });
            policy.Statements.Add(permissionStatement);

            var emptyStatements = new List<PolicyStatement>();

            var updated = policy.Update(addPolicyStatements: emptyStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsFalse(updated, "Policy was updated but shouldn't have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_policy_matching_doesnt_override_policy()
        {
            var policy = new Policy();
            var permissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[]
            {
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"
            });
            policy.Statements.Add(permissionStatement);

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsFalse(updated, "Policy was updated but shouldn't have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_policy_matching_but_different_order_doesnt_override_policy()
        {
            var policy = new Policy();
            var permissionStatement = PolicyExtensions.CreatePermissionStatementForQueueMatching("arn:fakeQueue", new[]
            {
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-YetAnotherEvent",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-YetYetAnotherEvent",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"
            });
            policy.Statements.Add(permissionStatement);

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-YetAnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-YetAnotherEvent", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-YetYetAnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-YetYetAnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsFalse(updated, "Policy was updated but shouldn't have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_legacy_policy_does_migrate_policy()
        {
            var policy = new Policy();
            policy.Statements.Add(PolicyStatement.CreatePermissionStatement("arn:fakeQueue", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event"));
            policy.Statements.Add(PolicyStatement.CreatePermissionStatement("arn:fakeQueue", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"));

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_partial_legacy_policy_does_migrate_policy()
        {
            var policy = new Policy();
            policy.Statements.Add(PolicyStatement.CreatePermissionStatement("arn:fakeQueue", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"));

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: false,
                addTopicNamePrefixConditionForPolicies: false,
                namespaceConditionsForPolicies: new List<string>(),
                topicNamePrefix: string.Empty,
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        [Test]
        public void Update_with_existing_legacy_policy_with_conditions_does_migrate_to_wildcard_policy()
        {
            var policy = new Policy();
            policy.Statements.Add(PolicyStatement.CreatePermissionStatement("arn:fakeQueue", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event"));
            policy.Statements.Add(PolicyStatement.CreatePermissionStatement("arn:fakeQueue", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"));

            var addStatements = new List<PolicyStatement>
            {
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", "arn:fakeQueue"),
                new PolicyStatement("DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:aws:sns:us-west-2:123456789012:DEV-NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent", "arn:fakeQueue")
            };

            var updated = policy.Update(addPolicyStatements: addStatements,
                addAccountConditionForPolicies: true,
                addTopicNamePrefixConditionForPolicies: true,
                namespaceConditionsForPolicies: new List<string>
                {
                    "NServiceBus.Transport.SQS.Tests.SubscriptionManagerTests."
                },
                topicNamePrefix: "DEV-",
                sqsQueueArn: "arn:fakeQueue");

            Assert.IsTrue(updated, "Policy was not updated but should have been");
            Approver.Verify(value: policy.ToJson(), ScrubPolicy);
        }

        private string ScrubPolicy(string policyAsString)
        {
            var scrubbed = Regex.Replace(policyAsString, "\"Sid\" : \"(.*)\",", string.Empty);
            return RemoveUnnecessaryWhiteSpace(scrubbed);
        }

        private static string RemoveUnnecessaryWhiteSpace(string policyAsString)
        {
            return string.Join(Environment.NewLine, policyAsString.Split(new[]
                {
                    Environment.NewLine
                }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.TrimEnd())
            );
        }
    }
}
#pragma warning restore 618