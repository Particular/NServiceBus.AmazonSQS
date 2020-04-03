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

            var metadata = messageMetadataRegistry.GetMessageMetadata(eventType);
            await SetupTypeSubscriptions(metadata, queueUrl).ConfigureAwait(false);
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
        }

        async Task DeleteSubscription(MessageMetadata metadata)
        {
            var mappedTopicsNames = topicCache.CustomEventToTopicsMappings.GetMappedTopicsNames(metadata.MessageType);
            foreach (var mappedTopicName in mappedTopicsNames)
            {
                //we skip the topic name generation assuming the topic name is already good
                var mappedTypeMatchingSubscription = await snsClient.FindMatchingSubscription(queueCache, mappedTopicName, queueName)
                    .ConfigureAwait(false);
                if (mappedTypeMatchingSubscription != null)
                {
                    await snsClient.UnsubscribeAsync(mappedTypeMatchingSubscription).ConfigureAwait(false);
                }
            }

            var mappedTypes = topicCache.CustomEventToEventsMappings.GetMappedTypes(metadata.MessageType);
            foreach (var mappedType in mappedTypes)
            {
                // forces the types need to match the convention
                var mappedTypeMetadata = messageMetadataRegistry.GetMessageMetadata(mappedType);
                if (mappedTypeMetadata == null)
                {
                    continue;
                }

                var mappedTypeMatchingSubscription = await snsClient.FindMatchingSubscription(queueCache, topicCache, mappedTypeMetadata, queueName)
                    .ConfigureAwait(false);
                if (mappedTypeMatchingSubscription != null)
                {
                    await snsClient.UnsubscribeAsync(mappedTypeMatchingSubscription).ConfigureAwait(false);
                }
            }

            var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, topicCache, metadata, queueName)
                .ConfigureAwait(false);
            if (matchingSubscriptionArn != null)
            {
                await snsClient.UnsubscribeAsync(matchingSubscriptionArn).ConfigureAwait(false);
            }

            MarkTypeNotConfigured(metadata.MessageType);
        }

        async Task SetupTypeSubscriptions(MessageMetadata metadata, string queueUrl)
        {
            var mappedTopicsNames = topicCache.CustomEventToTopicsMappings.GetMappedTopicsNames(metadata.MessageType);
            foreach (var mappedTopicName in mappedTopicsNames)
            {
                //we skip the topic name generation assuming the topic name is already good
                await CreateTopicAndSubscribe(mappedTopicName, queueUrl).ConfigureAwait(false);
            }

            var mappedTypes = topicCache.CustomEventToEventsMappings.GetMappedTypes(metadata.MessageType);
            foreach (var mappedType in mappedTypes)
            {
                // forces the types need to match the convention
                var mappedTypeMetadata = messageMetadataRegistry.GetMessageMetadata(mappedType);
                if (mappedTypeMetadata == null)
                {
                    continue;
                }

                // doesn't need to be cached since we never publish to it
                await CreateTopicAndSubscribe(mappedTypeMetadata, queueUrl).ConfigureAwait(false);
            }

            if (metadata.MessageType == typeof(object) || IsTypeTopologyKnownConfigured(metadata.MessageType))
            {
                return;
            }

            await CreateTopicAndSubscribe(metadata, queueUrl).ConfigureAwait(false);
            MarkTypeConfigured(metadata.MessageType);
        }

        async Task CreateTopicAndSubscribe(string topicName, string queueUrl)
        {
            var foundTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            if (foundTopic == null)
            {
                await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
                Logger.Debug($"Created topic topic '{topicName}'");
                foundTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }

            await SubscribeTo(foundTopic, topicName, queueUrl).ConfigureAwait(false);
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

            await SubscribeTo(topic, topicName, queueUrl).ConfigureAwait(false);
        }

        async Task SubscribeTo(Topic topic, string topicName, string queueUrl)
        {
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