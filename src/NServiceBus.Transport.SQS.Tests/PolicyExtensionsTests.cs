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