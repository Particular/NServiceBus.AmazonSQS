#pragma warning disable 1591

namespace NServiceBus
{
    using System;
    using Amazon.S3;

    public static partial class SqsTransportSettings
    {
        [ObsoleteEx(
            Message = "The SQS transport no longer supports 1.x compatibility mode",
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
        [ObsoleteEx(
            Message = "The SQS transport no longer supports 1.x compatibility mode",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public bool EnableV1CompatibilityMode { get; set; }
    }
}
#pragma warning restore 1591