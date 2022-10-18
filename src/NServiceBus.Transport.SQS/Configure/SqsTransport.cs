namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
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
    public class SqsTransport : TransportDefinition
    {
        /// <summary>
        /// SQS client for the transport.
        /// </summary>
        public IAmazonSQS SqsClient { get; internal set; } //For legacy API shim

        /// <summary>
        /// SNS client for the transport.
        /// </summary>
        public IAmazonSimpleNotificationService SnsClient { get; internal set; } //For legacy API shim

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SQS queue
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        public string QueueNamePrefix { get; set; }

        /// <summary>
        /// Specifies a lambda function that allows to take control of the queue name generation logic.
        /// This is useful to overcome any limitations imposed by SQS.
        /// </summary>
        public Func<string, string, string> QueueNameGenerator
        {
            get => queueNameGenerator;
            set
            {
                Guard.AgainstNull("value", value);
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
                Guard.AgainstNull("value", value);
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
                Guard.AgainstNull("value", value);
                topicNameGenerator = value;
            }
        }


        /// <summary>
        /// Configures the SQS transport to be compatible with 1.x versions of the transport.
        /// </summary>
        public bool EnableV1CompatibilityMode { get; set; }

        /// <summary>
        /// Configures the SQS transport to use S3 to store payload of large messages.
        /// </summary>
        public S3Settings S3 { get; set; }

        /// <summary>
        /// Configures the policy creation during subscription.
        /// </summary>
        public PolicySettings Policies { get; } = new PolicySettings();

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
            Guard.AgainstNull(nameof(customTopicsNames), customTopicsNames);
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
            Guard.AgainstNull(nameof(subscribedEventType), subscribedEventType);
            Guard.AgainstNull(nameof(publishedEventType), publishedEventType);

            eventToEventsMappings.Add(subscribedEventType, publishedEventType);
        }

        /// <summary>
        /// Creates a new instance of the SQS transport definition.
        /// </summary>
        public SqsTransport(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient)
            : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
            SqsClient = sqsClient;
            SnsClient = snsClient;
        }

        /// <summary>
        /// Creates a new instance of the SQS transport definition.
        ///
        /// Uses SQS and SNS clients created using a default constructor (based on the the settings from the environment)
        /// </summary>
        public SqsTransport()
            : base(TransportTransactionMode.ReceiveOnly, true, true, true)
        {
            SqsClient = new AmazonSQSClient();
            SnsClient = new AmazonSimpleNotificationServiceClient();
        }

        internal SqsTransport(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, bool supportsPublishSubscribe)
            : base(TransportTransactionMode.ReceiveOnly, true, supportsPublishSubscribe, true)
        {
            SqsClient = sqsClient;
            SnsClient = snsClient;
        }

        /// <summary>
        /// Initializes all the factories and supported features for the transport. This method is called right before all features
        /// are activated and the settings will be locked down. This means you can use the SettingsHolder both for providing
        /// default capabilities as well as for initializing the transport's configuration based on those settings (the user cannot
        /// provide information anymore at this stage).
        /// </summary>
        public override async Task<TransportInfrastructure> Initialize(HostSettings hostSettings, ReceiveSettings[] receivers, string[] sendingAddresses, CancellationToken cancellationToken = default)
        {
            var topicCache = new TopicCache(SnsClient, hostSettings.CoreSettings, eventToTopicsMappings, eventToEventsMappings, topicNameGenerator, topicNamePrefix);
            var infra = new SqsTransportInfrastructure(this, hostSettings, receivers, SqsClient, SnsClient, QueueCache, topicCache, S3, Policies, QueueDelayTime, topicNamePrefix, EnableV1CompatibilityMode);

            if (DeployInfrastructure)
            {
                var queueCreator = new QueueCreator(SqsClient, QueueCache, S3, maxTimeToLive, QueueDelayTime);

                var createQueueTasks = sendingAddresses.Select(x => queueCreator.CreateQueueIfNecessary(x, false))
                    .Concat(infra.Receivers.Values.Select(x => queueCreator.CreateQueueIfNecessary(x.ReceiveAddress, true))).ToArray();

                await Task.WhenAll(createQueueTasks).ConfigureAwait(false);
            }

            return infra;
        }

        /// <summary>
        /// Translates a <see cref="T:NServiceBus.Transport.QueueAddress" /> object into a transport specific queue address-string.
        /// </summary>
        [ObsoleteEx(Message = "Inject the ITransportAddressResolver type to access the address translation mechanism at runtime. See the NServiceBus version 8 upgrade guide for further details.",
                    TreatAsErrorFromVersion = "7",
                    RemoveInVersion = "8")]
#pragma warning disable CS0672 // Member overrides obsolete member
        public override string ToTransportAddress(QueueAddress address)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            var queueName = address.BaseAddress;
            var queue = new StringBuilder(queueName);
            if (address.Discriminator != null)
            {
                queue.Append("-" + address.Discriminator);
            }

            if (address.Qualifier != null)
            {
                queue.Append("-" + address.Qualifier);
            }

            return QueueCache.GetPhysicalQueueName(queue.ToString());
        }

        QueueCache QueueCache =>
            queueCache ??= new QueueCache(SqsClient,
                destination => queueNameGenerator(destination, QueueNamePrefix));

        /// <summary>
        /// Determines if the transport should try to create the
        /// required resources (queues, topics, subscriptions, etc.).
        /// The default value is <c>true</c>.
        /// </summary>
        internal bool DeployInfrastructure { get; set; } = true;

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

        static readonly TransportTransactionMode[] SupportedTransactionModes = {
            TransportTransactionMode.None,
            TransportTransactionMode.ReceiveOnly
        };

        static readonly TimeSpan MaxTimeToLiveUpperBound = TimeSpan.FromDays(14);
        static readonly TimeSpan MaxTimeToLiveLowerBound = TimeSpan.FromSeconds(60);
    }
}