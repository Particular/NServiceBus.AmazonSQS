namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using AmazonSQS;
    using Extensibility;
    using Logging;
    using Transport;
    using Unicast.Messages;

    class SubscriptionManager : IManageSubscriptions
    {
        public SubscriptionManager(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueCache queueCache, MessageMetadataRegistry messageMetadataRegistry, TopicCache topicCache)
        {
            this.topicCache = topicCache;
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.queueCache = queueCache;
            this.sqsClient = sqsClient;
            this.snsClient = snsClient;
            this.queueName = queueName;
        }

        public async Task Subscribe(Type eventType, ContextBag context)
        {
            var queueUrl = await queueCache.GetQueueUrl(queueName)
                .ConfigureAwait(false);

            await SetupTypeSubscriptions(eventType, queueUrl).ConfigureAwait(false);
        }

        public async Task Unsubscribe(Type eventType, ContextBag context)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(eventType);
            // not checking the topology cache
            if (metadata.MessageType == typeof(object))
            {
                return;
            }

            await DeleteSubscription(metadata).ConfigureAwait(false);

            MarkTypeNotConfigured(metadata.MessageType);
        }

        async Task DeleteSubscription(MessageMetadata metadata)
        {
            var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, topicCache, metadata, queueName)
                .ConfigureAwait(false);
            if (matchingSubscriptionArn != null)
            {
                await snsClient.UnsubscribeAsync(matchingSubscriptionArn).ConfigureAwait(false);
            }
        }

        async Task SetupTypeSubscriptions(Type eventType, string queueUrl)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(eventType);
            if (metadata.MessageType == typeof(object) || IsTypeTopologyKnownConfigured(metadata.MessageType))
            {
                return;
            }

            await CreateTopicAndSubscribe(metadata, queueUrl).ConfigureAwait(false);

            MarkTypeConfigured(metadata.MessageType);
        }

        async Task CreateTopicAndSubscribe(MessageMetadata metadata, string queueUrl)
        {
            string topicName = null;
            var topic = await topicCache.CreateIfNotExistent(metadata, name =>
            {
                topicName = name;
                Logger.Debug($"Created topic topic '{topicName}'");
            }).ConfigureAwait(false);

            topicName = topicName ?? topicCache.GetTopicName(metadata);

            try
            {
                // need to safe guard the subscribe section so that policy are not overriden
                // deliberately not set a cancellation token for now
                // https://github.com/aws/aws-sdk-net/issues/1569
                await subscribeQueueLimiter.WaitAsync().ConfigureAwait(false);

                // SNS dedups subscriptions based on the endpoint name
                // only the overload that takes the sqs client properly works with raw mode
                var createdSubscription = await snsClient.SubscribeQueueAsync(topic.TopicArn, sqsClient, queueUrl).ConfigureAwait(false);
                var setSubscriptionAttributesRequest = new SetSubscriptionAttributesRequest
                {
                    SubscriptionArn = createdSubscription,
                    AttributeName = "RawMessageDelivery",
                    AttributeValue = "true"
                };
                await snsClient.SetSubscriptionAttributesAsync(setSubscriptionAttributesRequest).ConfigureAwait(false);
            }
            finally
            {
                subscribeQueueLimiter.Release();
            }

            Logger.Debug($"Created subscription for queue '{queueName}' to topic '{topicName}'");
        }

        void MarkTypeConfigured(Type eventType)
        {
            typeTopologyConfiguredSet[eventType] = null;
        }

        void MarkTypeNotConfigured(Type eventType)
        {
            typeTopologyConfiguredSet.TryRemove(eventType, out _);
        }

        bool IsTypeTopologyKnownConfigured(Type eventType) => typeTopologyConfiguredSet.ContainsKey(eventType);

        readonly ConcurrentDictionary<Type, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Type, string>();
        readonly QueueCache queueCache;
        readonly IAmazonSQS sqsClient;
        readonly IAmazonSimpleNotificationService snsClient;
        readonly string queueName;
        readonly MessageMetadataRegistry messageMetadataRegistry;
        readonly TopicCache topicCache;
        readonly SemaphoreSlim subscribeQueueLimiter = new SemaphoreSlim(1);

        static ILog Logger = LogManager.GetLogger(typeof(SubscriptionManager));
    }
}