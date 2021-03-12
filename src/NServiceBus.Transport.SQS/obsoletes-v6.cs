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
            Message = "The native delayed delivery is always enabled in version 6.",
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

    /// <summary>
    /// Provides support for <see cref="UseTransport{T}"/> transport APIs.
    /// </summary>
    public static class SqsTransportApiExtensions
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [ObsoleteEx(
            RemoveInVersion = "10",
            TreatAsErrorFromVersion = "9",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static SqsTransportLegacySettings UseTransport<T>(this EndpointConfiguration config)
            where T : SqsTransport
        {
            var transport = new SqsTransport();

            var routing = config.UseTransport(transport);

            var settings = new SqsTransportLegacySettings(transport, routing);

            return settings;
        }
    }

    /// <summary>
    /// SQS transport configuration settings.
    /// </summary>
    public class SqsTransportLegacySettings : TransportSettings<SqsTransport>
    {
        internal SqsTransport SqsTransport { get; } //for testing the shim

        internal SqsTransportLegacySettings(SqsTransport transport, RoutingSettings<SqsTransport> routing)
            : base(transport, routing)

        {
            SqsTransport = transport;
        }

        [ObsoleteEx(
            Message = @"The compatibility mode is deprecated. Switch to native publish/subscribe mode using SNS instead.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public SubscriptionMigrationModeSettings EnableMessageDrivenPubSubCompatibilityMode()
        {
            throw new NotImplementedException();
        }

        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.QueueDelay",
            Message = "The native delayed delivery is always enabled in version 6.",
            TreatAsErrorFromVersion = "6",
            RemoveInVersion = "7")]
        public SqsTransportLegacySettings UnrestrictedDurationDelayedDelivery()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Configures the transport to use a custom SQS client.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport constructor",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings ClientFactory(Func<IAmazonSQS> factory)
        {
            Transport.SqsClient = factory();
            return this;
        }

        /// <summary>
        /// Configures the transport to use a custom SNS client.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport constructor",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings ClientFactory(Func<IAmazonSimpleNotificationService> factory)
        {
            Transport.SnsClient = factory();
            return this;
        }

        /// <summary>
        /// This is the maximum time that a message will be retained within SQS
        /// and S3. If you send a message, and that message is not received and successfully
        /// processed within the specified time, the message will be lost. This value applies
        /// to both SQS and S3 - messages in SQS will be deleted after this amount of time
        /// expires, and large message bodies stored in S3 will automatically be deleted
        /// after this amount of time expires.
        /// </summary>
        /// <remarks>
        /// If not specified, the endpoint uses a max TTL of 4 days.
        /// </remarks>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MaxTimeToLive",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings MaxTimeToLive(TimeSpan maxTimeToLive)
        {
            Transport.MaxTimeToLive = maxTimeToLive;
            return this;
        }

        /// <summary>
        /// Configures the SQS transport to use S3 to store payload of large messages.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.S3",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public S3Settings S3(string bucketForLargeMessages, string keyPrefix)
        {
            Transport.S3 = new S3Settings(bucketForLargeMessages, keyPrefix);
            return Transport.S3;
        }

        /// <summary>
        /// Configures the policy creation during subscription.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.Policies",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public PolicySettings Policies()
        {
            return Transport.Policies;
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SQS queue
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.QueueNamePrefix",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings QueueNamePrefix(string queueNamePrefix)
        {
            Transport.QueueNamePrefix = queueNamePrefix;
            return this;
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SNS topic
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.TopicNamePrefix",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings TopicNamePrefix(string topicNamePrefix)
        {
            Transport.TopicNamePrefix = topicNamePrefix;
            return this;
        }

        /// <summary>
        /// Specifies a lambda function that allows to take control of the topic generation logic.
        /// This is useful to overcome any limitations imposed by SNS, e.g. maximum topic name length.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.TopicNameGenerator",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings TopicNameGenerator(Func<Type, string, string> topicNameGenerator)
        {
            Transport.TopicNameGenerator = topicNameGenerator;
            return this;
        }

        /// <summary>
        /// Configures the SQS transport to be compatible with 1.x versions of the transport.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.EnableV1CompatibilityMode",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public SqsTransportLegacySettings EnableV1CompatibilityMode()
        {
            Transport.EnableV1CompatibilityMode = true;
            return this;
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void MapEvent<TSubscribedEvent>(string customTopicName)
        {
            Transport.MapEvent<TSubscribedEvent>(customTopicName);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void MapEvent(Type eventType, string customTopicName)
        {
            Transport.MapEvent(eventType, customTopicName);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void MapEvent<TSubscribedEvent>(IEnumerable<string> customTopicsNames)
        {
            Transport.MapEvent<TSubscribedEvent>(customTopicsNames);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void MapEvent(Type subscribedEventType, IEnumerable<string> customTopicsNames)
        {
            Transport.MapEvent(subscribedEventType, customTopicsNames);
        }

        /// <summary>
        /// Maps a specific message type to a concrete message type. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void MapEvent<TSubscribedEvent, TPublishedEvent>()
        {
            Transport.MapEvent<TSubscribedEvent, TPublishedEvent>();
        }

        /// <summary>
        /// Maps a specific message type to a concrete message type. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [ObsoleteEx(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public void MapEvent(Type subscribedEventType, Type publishedEventType)
        {
            Transport.MapEvent(subscribedEventType, publishedEventType);
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
#pragma warning restore 618