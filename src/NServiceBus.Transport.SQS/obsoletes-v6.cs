#pragma warning disable 1591
#pragma warning disable 618

namespace NServiceBus
{
    using Amazon.S3;
    using System;
    using System.Collections.Generic;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;


    public static class SqsTransportSettings
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
            ReplacementTypeOrMember = "SqsTransport constructor",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> ClientFactory(this TransportExtensions<SqsTransport> transportExtensions, Func<IAmazonSQS> factory)
        {
            throw new NotImplementedException();
        }


        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport constructor",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> ClientFactory(this TransportExtensions<SqsTransport> transportExtensions, Func<IAmazonSimpleNotificationService> factory)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MaxTimeToLive",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> MaxTimeToLive(this TransportExtensions<SqsTransport> transportExtensions, TimeSpan maxTimeToLive)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.S3",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static S3Settings S3(this TransportExtensions<SqsTransport> transportExtensions, string bucketForLargeMessages, string keyPrefix)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.Policies",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static PolicySettings Policies(this TransportExtensions<SqsTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.QueueNamePrefix",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> QueueNamePrefix(this TransportExtensions<SqsTransport> transportExtensions, string queueNamePrefix)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.TopicNamePrefix",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> TopicNamePrefix(this TransportExtensions<SqsTransport> transportExtensions, string topicNamePrefix)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.TopicNameGenerator",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> TopicNameGenerator(this TransportExtensions<SqsTransport> transportExtensions, Func<Type, string, string> topicNameGenerator)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.QueueDelay",
            Message = "The native delayed delivery is always enabled in version 6. The QueueDelay time can be configured via SqsTransport.QueueDelay property.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> UnrestrictedDurationDelayedDelivery(this TransportExtensions<SqsTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.EnableV1CompatibilityMode",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static TransportExtensions<SqsTransport> EnableV1CompatibilityMode(this TransportExtensions<SqsTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static void MapEvent<TSubscribedEvent>(this TransportExtensions<SqsTransport> transportExtensions, string customTopicName)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static void MapEvent(this TransportExtensions<SqsTransport> transportExtensions, Type eventType, string customTopicName)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static void MapEvent<TSubscribedEvent>(this TransportExtensions<SqsTransport> transportExtensions, IEnumerable<string> customTopicsNames)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static void MapEvent(this TransportExtensions<SqsTransport> transportExtensions, Type subscribedEventType, IEnumerable<string> customTopicsNames)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static void MapEvent<TSubscribedEvent, TPublishedEvent>(this TransportExtensions<SqsTransport> transportExtensions)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public static void MapEvent(this TransportExtensions<SqsTransport> transportExtensions, Type subscribedEventType, Type publishedEventType)
        {
            throw new NotImplementedException();
        }

    }

    public partial class S3Settings
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "S3Settings constructor",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public void ClientFactory(Func<IAmazonS3> factory)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "Encryption",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public void ServerSideEncryption(ServerSideEncryptionMethod encryptionMethod, string keyManagementServiceKeyId = null)
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "Encryption",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        // ReSharper disable InconsistentNaming
        public void ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod encryptionMethod, string providedKey, string providedKeyMD5 = null)
        // ReSharper restore InconsistentNaming
        {
            throw new NotImplementedException();

        }
    }

    public partial class PolicySettings
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = "SetupTopicPoliciesWhenSubscribing",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public void AssumePolicyHasAppropriatePermissions()
        {
            throw new NotImplementedException();
        }


        [ObsoleteEx(
            ReplacementTypeOrMember = "AccountCondition",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public void AddAccountCondition()
        {
            throw new NotImplementedException();
        }


        [ObsoleteEx(
            ReplacementTypeOrMember = "TopicNamePrefixCondition",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public void AddTopicNamePrefixCondition()
        {
            throw new NotImplementedException();
        }


        [ObsoleteEx(
            ReplacementTypeOrMember = "TopicNamespaceConditions",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public void AddNamespaceCondition(string topicNamespace)
        {
            throw new NotImplementedException();
        }
    }
}
#pragma warning restore 1591
#pragma warning restore 618