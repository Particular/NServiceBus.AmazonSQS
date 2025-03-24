﻿namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Transport;
    using Transport.SQS;
    using Transport.SQS.Configure;
    using TransportInfrastructure = Transport.TransportInfrastructure;

    /// <summary>
    /// Sqs transport definition.
    /// </summary>
    public partial class SqsTransport : TransportDefinition
    {
        /// <summary>
        /// SQS client for the transport.
        /// </summary>
        public IAmazonSQS SqsClient => sqsClient.Instance;

        /// <summary>
        /// SNS client for the transport.
        /// </summary>
        public IAmazonSimpleNotificationService SnsClient => snsClient.Instance;

        internal void SetupSqsClient(IAmazonSQS sqsClient, bool externallyManaged)
        {
            ArgumentNullException.ThrowIfNull(sqsClient);
            this.sqsClient = (sqsClient, externallyManaged);
        }

        internal void SetupSnsClient(IAmazonSimpleNotificationService snsClient, bool externallyManaged)
        {
            ArgumentNullException.ThrowIfNull(snsClient);
            this.snsClient = (snsClient, externallyManaged);
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SQS queue
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        public string QueueNamePrefix { get; set; }

        /// <summary>
        /// Specifies an arbitrary number of bytes that will be added to the calculated payload size
        /// which is useful to account for any overhead of message attributes determining whether
        /// the message payload will be stored in S3 or not.
        /// </summary>
        public long PayloadPaddingInBytes { get; set; }

        /// <summary>
        /// Specifies a lambda function that allows to take control of the queue name generation logic.
        /// This is useful to overcome any limitations imposed by SQS.
        /// </summary>
        public Func<string, string, string> QueueNameGenerator
        {
            get => queueNameGenerator;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                queueNameGenerator = value;
            }
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
        public TimeSpan MaxTimeToLive
        {
            get => maxTimeToLive;
            set
            {
                if (maxTimeToLive <= MaxTimeToLiveLowerBound || maxTimeToLive > MaxTimeToLiveUpperBound)
                {
                    throw new ArgumentException($"Max TTL needs to be between {MaxTimeToLiveLowerBound} and {MaxTimeToLiveUpperBound}.");
                }

                maxTimeToLive = value;
            }
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SNS topic
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        public string TopicNamePrefix
        {
            get => topicNamePrefix;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                topicNamePrefix = value;
            }
        }

        /// <summary>
        /// Specifies a lambda function that allows to take control of the topic generation logic.
        /// This is useful to overcome any limitations imposed by SNS, e.g. maximum topic name length.
        /// </summary>
        public Func<Type, string, string> TopicNameGenerator
        {
            get => topicNameGenerator;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                topicNameGenerator = value;
            }
        }

        /// <summary>
        /// Configures the SQS transport to use S3 to store payload of large messages.
        /// </summary>
        public S3Settings S3 { get; set; }

        /// <summary>
        /// Configures the policy creation during subscription.
        /// </summary>
        public PolicySettings Policies { get; } = new PolicySettings();

        /// <summary>
        /// Configures the SQS transport to not use a custom wrapper for outgoing messages.
        /// NServiceBus headers will be sent as an Amazon message attribute.
        /// Only turn this on if all your endpoints are version 6.1.0 or above.
        /// </summary>
        /// <remarks>In cases when the outgoing message contains characters that are not compliant with the <see href="https://www.w3.org/TR/REC-xml/#charsets">W3C specification
        /// for characters</see> <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_SendMessage.html">SQS requires</see> the payload is base64 encoded automatically.</remarks>
        public bool DoNotWrapOutgoingMessages { get; set; }

        /// <summary>
        /// Configures the delay time to use (up to 15 minutes) when messages are delayed. If message is delayed for longer than
        /// 15 minutes, it is bounced back to the delay queue until it is due.
        ///
        /// This is only for acceptance tests
        /// </summary>
        internal int QueueDelayTime { get; set; } = 15 * 60;

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        public void MapEvent<TSubscribedEvent>(string customTopicName)
        {
            MapEvent(typeof(TSubscribedEvent), new[] { customTopicName });
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        public void MapEvent(Type eventType, string customTopicName)
        {
            MapEvent(eventType, new[] { customTopicName });
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        public void MapEvent<TSubscribedEvent>(IEnumerable<string> customTopicsNames)
        {
            MapEvent(typeof(TSubscribedEvent), customTopicsNames);
        }

        /// <summary>
        /// Maps a specific message type to a set of topics. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        public void MapEvent(Type subscribedEventType, IEnumerable<string> customTopicsNames)
        {
            ArgumentNullException.ThrowIfNull(customTopicsNames);
            eventToTopicsMappings.Add(subscribedEventType, customTopicsNames);
        }

        /// <summary>
        /// Maps a specific message type to a concrete message type. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        public void MapEvent<TSubscribedEvent, TPublishedEvent>()
        {
            MapEvent(typeof(TSubscribedEvent), typeof(TPublishedEvent));
        }

        /// <summary>
        /// Maps a specific message type to a concrete message type. The transport will automatically map the most concrete type to a topic.
        /// In case a subscriber needs to subscribe to a type up in the message inheritance chain a custom mapping needs to be defined.
        /// </summary>
        public void MapEvent(Type subscribedEventType, Type publishedEventType)
        {
            ArgumentNullException.ThrowIfNull(subscribedEventType);
            ArgumentNullException.ThrowIfNull(publishedEventType);

            eventToEventsMappings.Add(subscribedEventType, publishedEventType);
        }

        /// <summary>
        /// Creates a new instance of the SQS transport definition.
        /// </summary>
        public SqsTransport(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient)
            : this(sqsClient, snsClient, externallyManaged: true)
        {
        }

        /// <summary>
        /// Creates a new instance of the SQS transport definition.
        ///
        /// Uses SQS and SNS clients created using a default constructor (based on the the settings from the environment)
        /// </summary>
        public SqsTransport()
            : this(DefaultClientFactories.SqsFactory(), DefaultClientFactories.SnsFactory(), externallyManaged: false)
        {
        }

        /// <summary>
        /// Creates a new instance of the SQS transport definition.
        /// </summary>
        [Experimental(DiagnosticDescriptors.ExperimentalDisableDelayedDelivery)]
        public SqsTransport(
            IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient,
            bool enableDelayedDelivery
        )
            : base(
                TransportTransactionMode.ReceiveOnly,
                supportsDelayedDelivery: enableDelayedDelivery,
                supportsPublishSubscribe: true,
                supportsTTBR: true
            )
        {
            SetupSqsClient(sqsClient, true);
            SetupSnsClient(snsClient, true);
        }

        // Only invoke when not using external SQS and SNS clients
        internal SqsTransport(
            IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient,
            bool externallyManaged,
            bool supportsPublishSubscribe = true,
            bool enableDelayedDelivery = true
        )
            : base(
                TransportTransactionMode.ReceiveOnly,
                enableDelayedDelivery,
                supportsPublishSubscribe,
                supportsTTBR: true
            )
        {
            SetupSqsClient(sqsClient, externallyManaged);
            SetupSnsClient(snsClient, externallyManaged);
        }

        /// <summary>
        /// Initializes all the factories and supported features for the transport. This method is called right before all features
        /// are activated and the settings will be locked down. This means you can use the SettingsHolder both for providing
        /// default capabilities as well as for initializing the transport's configuration based on those settings (the user cannot
        /// provide information anymore at this stage).
        /// </summary>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            AssertQueueNameGeneratorIdempotent(queueNameGenerator);

            var topicCache = new TopicCache(
                SnsClient,
                hostSettings.CoreSettings,
                eventToTopicsMappings,
                eventToEventsMappings,
                topicNameGenerator,
                topicNamePrefix
            );

            var infra = new SqsTransportInfrastructure(
                hostSettings,
                receivers,
                SqsClient,
                SnsClient,
                QueueCache,
                topicCache,
                S3,
                Policies,
                QueueDelayTime,
                topicNamePrefix,
                DoNotWrapOutgoingMessages,
                !sqsClient.ExternallyManaged,
                !snsClient.ExternallyManaged,
                !SupportsDelayedDelivery,
                PayloadPaddingInBytes
            );

            if (hostSettings.SetupInfrastructure)
            {
                var queueCreator = new QueueCreator(SqsClient, QueueCache, S3, maxTimeToLive, QueueDelayTime);

                var createQueueTasks = sendingAddresses.Select(x => queueCreator.CreateQueueIfNecessary(x, false, cancellationToken))
                    .Concat(infra.Receivers.Values.Select(x => queueCreator.CreateQueueIfNecessary(x.ReceiveAddress, SupportsDelayedDelivery, cancellationToken))).ToArray();

                await Task.WhenAll(createQueueTasks).ConfigureAwait(false);
            }

            return infra;
        }

        static void AssertQueueNameGeneratorIdempotent(Func<string, string, string> generator)
        {
            const string prefix = "Prefix";
            const string destination = "Destination";

            var once = generator(destination, prefix);
            var twice = generator(once, prefix);
            if (once != twice)
            {
                throw new Exception($"The queue name generator function needs to return the same result when it is applied multiple times (idempotent). Result of applying once is {once} and twice -- {twice}.");
            }
        }

        QueueCache QueueCache =>
            queueCache ??= new QueueCache(SqsClient,
                destination => queueNameGenerator(destination, QueueNamePrefix));

        /// <summary>
        /// Returns a list of all supported transaction modes of this transport.
        /// </summary>
        public override IReadOnlyCollection<TransportTransactionMode> GetSupportedTransactionModes() => SupportedTransactionModes;

        QueueCache queueCache;
        TimeSpan maxTimeToLive = TimeSpan.FromDays(4);
        string topicNamePrefix;
        Func<Type, string, string> topicNameGenerator = TopicNameHelper.GetSnsTopicName;
        Func<string, string, string> queueNameGenerator = QueueCache.GetSqsQueueName;
        readonly EventToTopicsMappings eventToTopicsMappings = new EventToTopicsMappings();
        readonly EventToEventsMappings eventToEventsMappings = new EventToEventsMappings();

        static readonly TransportTransactionMode[] SupportedTransactionModes = { TransportTransactionMode.None, TransportTransactionMode.ReceiveOnly };

        static readonly TimeSpan MaxTimeToLiveUpperBound = TimeSpan.FromDays(14);
        static readonly TimeSpan MaxTimeToLiveLowerBound = TimeSpan.FromSeconds(60);
        (IAmazonSQS Instance, bool ExternallyManaged) sqsClient;
        (IAmazonSimpleNotificationService Instance, bool ExternallyManaged) snsClient;
    }
}