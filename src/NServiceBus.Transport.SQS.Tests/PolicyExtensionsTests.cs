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
    }
}
#pragma warning restore 618