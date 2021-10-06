#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Amazon.S3;

    public static partial class SqsTransportSettings
    {
        [ObsoleteEx(
            Message = @"The compatibility mode is deprecated. Switch to native publish/subscribe mode using SNS instead.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode(this TransportExtensions<SqsTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.QueueDelay",
            Message = "The native delayed delivery is always enabled in version 6.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> UnrestrictedDurationDelayedDelivery(this TransportExtensions<SqsTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }
    }

    public partial class S3Settings
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "Client or S3Settings constructor",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void ClientFactory(Func<IAmazonS3> factory)
        {
            S3Client = factory();
        }

        /// <summary>
        /// Configures the transport to use managed key encryption.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "Encryption",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void ServerSideEncryption(ServerSideEncryptionMethod encryptionMethod, string keyManagementServiceKeyId = null)
        {
            Encryption = new S3EncryptionWithManagedKey(encryptionMethod, keyManagementServiceKeyId);
        }

        /// <summary>
        /// Configures the transport to use customer key encryption.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "Encryption",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        // ReSharper disable InconsistentNaming
        public void ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod encryptionMethod, string providedKey, string providedKeyMD5 = null)
        // ReSharper restore InconsistentNaming
        {
            Encryption = new S3EncryptionWithCustomerProvidedKey(encryptionMethod, providedKey, providedKeyMD5);
        }
    }

    public partial class PolicySettings
    {
        /// <summary>
        /// Disable setting up the IAM policies for topics.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SetupTopicPoliciesWhenSubscribing",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AssumePolicyHasAppropriatePermissions()
        {
            SetupTopicPoliciesWhenSubscribing = false;
        }

        /// <summary>
        /// Adds an account wildcard condition for every account found on the topics subscribed to.
        /// </summary>
        /// <example>
        /// Subscribing to
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" : "arn:aws:sns:some-region:some-account:*" } }
        /// ]]>
        /// </example>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        [ObsoleteEx(
            ReplacementTypeOrMember = "AccountCondition",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AddAccountCondition()
        {
            AccountCondition = true;
        }

        /// <summary>
        /// Adds a topic name prefix wildcard condition.
        /// </summary>
        /// <example>
        /// <code>
        ///    transport.TopicNamePrefix = "DEV-"
        /// </code> and subscribing to
        /// - arn:aws:sns:some-region:some-account:DEV-Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:DEV-Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" : "arn:aws:sns:some-region:some-account:DEV-*" } }
        /// ]]>
        /// </example>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        [ObsoleteEx(
            ReplacementTypeOrMember = "TopicNamePrefixCondition",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AddTopicNamePrefixCondition()
        {
            TopicNamePrefixCondition = true;
        }

        /// <summary>
        /// Adds one or multiple topic namespace wildcard conditions.
        /// </summary>
        /// <example>
        /// <code>
        ///    var policies = transport.Policies;
        ///    policies.TopicNamespaceConditions.Add("Some.Namespace.");
        ///    policies.TopicNamespaceConditions.Add("SomeOther.Namespace");
        /// </code> and subscribing to
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-Event
        /// - arn:aws:sns:some-region:some-account:Some-Namespace-AnotherEvent
        /// would lead to
        /// <![CDATA[
        /// "Condition" : { "ArnLike" : { "aws:SourceArn" :
        ///    "arn:aws:sns:some-region:some-account:Some-Namespace-*",
        ///    "arn:aws:sns:some-region:some-account:SomeOther-Namespace*",
        /// } }
        /// ]]>
        /// </example>
        /// <remarks>It is possible to use dots in the provided namespace (for example Sales.VipCustomers.). The namespaces will be translated into a compliant format.</remarks>
        /// <remarks>Calling this method will opt-in for wildcard policy and no longer populate the policy with the explicit topic ARNs the endpoint subscribes to.</remarks>
        [ObsoleteEx(
            ReplacementTypeOrMember = "TopicNamespaceConditions",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AddNamespaceCondition(string topicNamespace)
        {
            TopicNamespaceConditions.Add(topicNamespace);
        }
    }
}
#pragma warning restore 1591