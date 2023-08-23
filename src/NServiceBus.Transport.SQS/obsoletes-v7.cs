#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Amazon.S3;
    using NServiceBus.Transport;

    public static partial class SqsTransportSettings
    {
        [ObsoleteEx(
            Message = "Configures the SQS transport to be compatible with 1.x versions of the transport.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> EnableV1CompatibilityMode(this TransportExtensions<SqsTransport> transportExtensions)
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
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "Encryption",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void ServerSideEncryption(ServerSideEncryptionMethod encryptionMethod, string keyManagementServiceKeyId = null)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "Encryption",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod encryptionMethod, string providedKey, string providedKeyMD5 = null)
        {
            throw new NotImplementedException();
        }
    }

    public partial class PolicySettings
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "SetupTopicPoliciesWhenSubscribing",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AssumePolicyHasAppropriatePermissions()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "AccountCondition",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AddAccountCondition()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "TopicNamePrefixCondition",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AddTopicNamePrefixCondition()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "TopicNamespaceConditions",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void AddNamespaceCondition(string topicNamespace)
        {
            throw new NotImplementedException();
        }
    }

    public partial class SqsTransport
    {
        [ObsoleteEx(Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
                    TreatAsErrorFromVersion = "7",
                    RemoveInVersion = "8")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(QueueAddress address)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            Message = "Configures the SQS transport to be compatible with 1.x versions of the transport.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public bool EnableV1CompatibilityMode { get; set; }
    }
}
#pragma warning restore 1591