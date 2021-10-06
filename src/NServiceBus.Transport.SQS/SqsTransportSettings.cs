namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;

    /// <summary>
    /// SQS transport configuration settings.
    /// </summary>
    public static partial class SqsTransportSettings
    {
        /// <summary>
        /// Configures NServiceBus to use the given transport.
        /// </summary>
        [PreObsolete(
            RemoveInVersion = "10",
            TreatAsErrorFromVersion = "9",
            ReplacementTypeOrMember = "EndpointConfiguration.UseTransport(TransportDefinition)")]
        public static TransportExtensions<SqsTransport> UseTransport<T>(this EndpointConfiguration config)
            where T : SqsTransport
        {
            var transport = new SqsTransport();

            var routing = config.UseTransport(transport);

            var settings = new TransportExtensions<SqsTransport>(transport, routing);

            return settings;
        }

        /// <summary>
        /// Configures the transport to use a custom SQS client.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport constructor",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> ClientFactory(this TransportExtensions<SqsTransport> transportExtensions, Func<IAmazonSQS> factory)
        {
            transportExtensions.Transport.SqsClient = factory();
            return transportExtensions;
        }

        /// <summary>
        /// Configures the transport to use a custom SNS client.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport constructor",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> ClientFactory(this TransportExtensions<SqsTransport> transportExtensions, Func<IAmazonSimpleNotificationService> factory)
        {
            transportExtensions.Transport.SnsClient = factory();
            return transportExtensions;
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
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MaxTimeToLive",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> MaxTimeToLive(this TransportExtensions<SqsTransport> transportExtensions, TimeSpan maxTimeToLive)
        {
            transportExtensions.Transport.MaxTimeToLive = maxTimeToLive;
            return transportExtensions;
        }

        /// <summary>
        /// Configures the SQS transport to use S3 to store payload of large messages.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.S3",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static S3Settings S3(this TransportExtensions<SqsTransport> transportExtensions, string bucketForLargeMessages, string keyPrefix)
        {
            transportExtensions.Transport.S3 = new S3Settings(bucketForLargeMessages, keyPrefix);
            return transportExtensions.Transport.S3;
        }

        /// <summary>
        /// Configures the policy creation during subscription.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.Policies",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static PolicySettings Policies(this TransportExtensions<SqsTransport> transportExtensions)
        {
            return transportExtensions.Transport.Policies;
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SQS queue
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.QueueNamePrefix",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> QueueNamePrefix(this TransportExtensions<SqsTransport> transportExtensions, string queueNamePrefix)
        {
            transportExtensions.Transport.QueueNamePrefix = queueNamePrefix;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SNS topic
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.TopicNamePrefix",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> TopicNamePrefix(this TransportExtensions<SqsTransport> transportExtensions, string topicNamePrefix)
        {
            transportExtensions.Transport.TopicNamePrefix = topicNamePrefix;
            return transportExtensions;
        }

        /// <summary>
        /// Specifies a lambda function that allows to take control of the topic generation logic.
        /// This is useful to overcome any limitations imposed by SNS, e.g. maximum topic name length.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.TopicNameGenerator",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> TopicNameGenerator(this TransportExtensions<SqsTransport> transportExtensions, Func<Type, string, string> topicNameGenerator)
        {
            transportExtensions.Transport.TopicNameGenerator = topicNameGenerator;
            return transportExtensions;
        }

        /// <summary>
        /// Configures the SQS transport to be compatible with 1.x versions of the transport.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.EnableV1CompatibilityMode",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static TransportExtensions<SqsTransport> EnableV1CompatibilityMode(this TransportExtensions<SqsTransport> transportExtensions)
        {
            transportExtensions.Transport.EnableV1CompatibilityMode = true;
            return transportExtensions;
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void MapEvent<TSubscribedEvent>(this TransportExtensions<SqsTransport> transportExtensions, string customTopicName)
        {
            transportExtensions.Transport.MapEvent<TSubscribedEvent>(customTopicName);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void MapEvent(this TransportExtensions<SqsTransport> transportExtensions, Type eventType, string customTopicName)
        {
            transportExtensions.Transport.MapEvent(eventType, customTopicName);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void MapEvent<TSubscribedEvent>(this TransportExtensions<SqsTransport> transportExtensions, IEnumerable<string> customTopicsNames)
        {
            transportExtensions.Transport.MapEvent<TSubscribedEvent>(customTopicsNames);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void MapEvent(this TransportExtensions<SqsTransport> transportExtensions, Type subscribedEventType, IEnumerable<string> customTopicsNames)
        {
            transportExtensions.Transport.MapEvent(subscribedEventType, customTopicsNames);
        }

        /// <summary>
        /// Maps a specific message type to a concrete message type. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void MapEvent<TSubscribedEvent, TPublishedEvent>(this TransportExtensions<SqsTransport> transportExtensions)
        {
            transportExtensions.Transport.MapEvent<TSubscribedEvent, TPublishedEvent>();
        }

        /// <summary>
        /// Maps a specific message type to a concrete message type. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        [PreObsolete(
            ReplacementTypeOrMember = "SqsTransport.MapEvent",
            Message = "The configuration has been moved to SqsTransport class.",
            TreatAsErrorFromVersion = "7",
            RemoveInVersion = "8")]
        public static void MapEvent(this TransportExtensions<SqsTransport> transportExtensions, Type subscribedEventType, Type publishedEventType)
        {
            transportExtensions.Transport.MapEvent(subscribedEventType, publishedEventType);
        }
    }
}
