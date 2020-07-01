namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using DelayedDelivery;
    using Logging;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;
    using Unicast.Messages;

    class SqsTransportInfrastructure : TransportInfrastructure
    {
        public SqsTransportInfrastructure(ReadOnlySettings settings)
        {
            this.settings = settings;
            messageMetadataRegistry = this.settings.Get<MessageMetadataRegistry>();
            configuration = new TransportConfiguration(settings);

            if (settings.HasSetting(SettingsKeys.DisableNativePubSub))
            {
                OutboundRoutingPolicy = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Unicast, OutboundRoutingType.Unicast);
            }
            else
            {
                OutboundRoutingPolicy = new OutboundRoutingPolicy(OutboundRoutingType.Unicast, OutboundRoutingType.Multicast, OutboundRoutingType.Unicast);
            }

            try
            {
                sqsClient = configuration.SqsClientFactory();
            }
            catch (AmazonClientException e)
            {
                var message = "Unable to configure the SQS client. Make sure the environment variables for AWS_REGION, AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are set or the client factory configures the created client accordingly";
                Logger.Error(message, e);
                throw new Exception(message, e);
            }

            try
            {
                snsClient = configuration.SnsClientFactory();
            }
            catch (AmazonClientException e)
            {
                var message = "Unable to configure the SNS client. Make sure the environment variables for AWS_REGION, AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are set or the client factory configures the created client accordingly";
                Logger.Error(message, e);
                throw new Exception(message, e);
            }

            try
            {
                if (!string.IsNullOrEmpty(settings.GetOrDefault<string>(SettingsKeys.S3BucketForLargeMessages)))
                {
                    s3Client = configuration.S3ClientFactory();
                }
            }
            catch (AmazonClientException e)
            {
                var message = "Unable to configure the S3 client. Make sure the environment variables for AWS_REGION, AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY are set or the client factory configures the created client accordingly";
                Logger.Error(message, e);
                throw new Exception(message, e);
            }

            queueCache = new QueueCache(sqsClient, configuration);
            topicCache = new TopicCache(snsClient, messageMetadataRegistry, configuration);
        }

        public SubscriptionManager SubscriptionManager { get; private set;  }

        public override IEnumerable<Type> DeliveryConstraints => new List<Type>
        {
            typeof(DiscardIfNotReceivedBefore),
            typeof(DoNotDeliverBefore),
            typeof(DelayDeliveryWith)
        };

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override OutboundRoutingPolicy OutboundRoutingPolicy { get; }

        MessagePump CreateMessagePump()
        {
            return new MessagePump(configuration, new InputQueuePump(configuration, s3Client, sqsClient, queueCache), new DelayedMessagesPump(configuration, sqsClient, queueCache));
        }

        QueueCreator CreateQueueCreator()
        {
            return new QueueCreator(configuration, s3Client, sqsClient, queueCache);
        }

        MessageDispatcher CreateMessageDispatcher()
        {
            return new MessageDispatcher(configuration, s3Client, sqsClient, snsClient, queueCache, topicCache);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                CreateMessagePump,
                CreateQueueCreator,
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                CreateMessageDispatcher,
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override Task Stop()
        {
            sqsClient.Dispose();
            snsClient.Dispose();
            s3Client?.Dispose();
            return base.Stop();
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            if (SubscriptionManager == null)
            {
                SubscriptionManager = new SubscriptionManager(sqsClient,
                    snsClient,
                    settings.LocalAddress(),
                    queueCache,
                    messageMetadataRegistry,
                    topicCache,
                    settings.HasSetting(SettingsKeys.DisableSubscribeBatchingOnStart));
            }
            return new TransportSubscriptionInfrastructure(() => SubscriptionManager);
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance;
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queueName = logicalAddress.EndpointInstance.Endpoint;
            var queue = new StringBuilder(queueName);
            if (logicalAddress.EndpointInstance.Discriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.Discriminator);
            }

            if (logicalAddress.Qualifier != null)
            {
                queue.Append("-" + logicalAddress.Qualifier);
            }

            return queueCache.GetPhysicalQueueName(queue.ToString());
        }

        readonly IAmazonSQS sqsClient;
        readonly IAmazonSimpleNotificationService snsClient;
        readonly IAmazonS3 s3Client;
        readonly QueueCache queueCache;
        readonly TransportConfiguration configuration;
        readonly ReadOnlySettings settings;
        readonly MessageMetadataRegistry messageMetadataRegistry;
        readonly TopicCache topicCache;
        static ILog Logger = LogManager.GetLogger(typeof(SqsTransportInfrastructure));
    }
}