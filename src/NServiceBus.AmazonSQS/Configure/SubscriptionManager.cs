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
                    Logger.Debug($"Removing subscription for queue '{queueName}' to topic '{mappedTopicName}'");
                    await snsClient.UnsubscribeAsync(mappedTypeMatchingSubscription).ConfigureAwait(false);
                    Logger.Debug($"Removed subscription for queue '{queueName}' to topic '{mappedTopicName}'");
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
                    Logger.Debug($"Removing subscription with arn '{mappedTypeMatchingSubscription}' for queue '{queueName}'");
                    await snsClient.UnsubscribeAsync(mappedTypeMatchingSubscription).ConfigureAwait(false);
                    Logger.Debug($"Removed subscription with arn '{mappedTypeMatchingSubscription}' for queue '{queueName}'");
                }
            }

            var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, topicCache, metadata, queueName)
                .ConfigureAwait(false);
            if (matchingSubscriptionArn != null)
            {
                Logger.Debug($"Removing subscription with arn '{matchingSubscriptionArn}' for queue '{queueName}'");
                await snsClient.UnsubscribeAsync(matchingSubscriptionArn).ConfigureAwait(false);
                Logger.Debug($"Removed subscription with arn '{matchingSubscriptionArn}' for queue '{queueName}'");
            }

            MarkTypeNotConfigured(metadata.MessageType);
        }

        async Task SetupTypeSubscriptions(MessageMetadata metadata, string queueUrl)
        {
            var mappedTopicsNames = topicCache.CustomEventToTopicsMappings.GetMappedTopicsNames(metadata.MessageType);
            foreach (var mappedTopicName in mappedTopicsNames)
            {
                //we skip the topic name generation assuming the topic name is already good
                Logger.Debug($"Creating topic/subscription to '{mappedTopicName}' for queue '{queueName}'");
                await CreateTopicAndSubscribe(mappedTopicName, queueUrl).ConfigureAwait(false);
                Logger.Debug($"Created topic/subscription to '{mappedTopicName}' for queue '{queueName}'");
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
                Logger.Debug($"Creating topic/subscription for '{mappedTypeMetadata.MessageType.FullName}' for queue '{queueName}'");
                await CreateTopicAndSubscribe(mappedTypeMetadata, queueUrl).ConfigureAwait(false);
                Logger.Debug($"Created topic/subscription for '{mappedTypeMetadata.MessageType.FullName}' for queue '{queueName}'");
            }

            if (metadata.MessageType == typeof(object) || IsTypeTopologyKnownConfigured(metadata.MessageType))
            {
                Logger.Debug($"Skipped subscription for '{metadata.MessageType.FullName}' for queue '{queueName}' because it is already configured");
                return;
            }

            Logger.Debug($"Creating topic/subscription for '{metadata.MessageType.FullName}' for queue '{queueName}'");
            await CreateTopicAndSubscribe(metadata, queueUrl).ConfigureAwait(false);
            Logger.Debug($"Created topic/subscription for '{metadata.MessageType.FullName}' for queue '{queueName}'");
            MarkTypeConfigured(metadata.MessageType);
        }

        async Task CreateTopicAndSubscribe(string topicName, string queueUrl)
        {
            Logger.Debug($"Getting or creating topic '{topicName}' for queue '{queueName}");
            var createTopicResponse = await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
            Logger.Debug($"Got or created topic '{topicName}' with arn '{createTopicResponse.TopicArn}' for queue '{queueName}");

            await SubscribeTo(createTopicResponse.TopicArn, topicName, queueUrl).ConfigureAwait(false);
        }

        async Task CreateTopicAndSubscribe(MessageMetadata metadata, string queueUrl)
        {
            var topicName = topicCache.GetTopicName(metadata);
            Logger.Debug($"Getting or creating topic '{topicName}' for queue '{queueName}");
            var createTopicResponse = await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
            Logger.Debug($"Got or created topic '{topicName}' with arn '{createTopicResponse.TopicArn}' for queue '{queueName}");

            await SubscribeTo(createTopicResponse.TopicArn, topicName, queueUrl).ConfigureAwait(false);
        }

        async Task SubscribeTo(string topicArn, string topicName, string queueUrl)
        {
            Logger.Debug($"Creating subscription for queue '{queueName}' to topic '{topicName}' with arn '{topicArn}'");
            try
            {
                // need to safe guard the subscribe section so that policy are not overriden
                // deliberately not set a cancellation token for now
                // https://github.com/aws/aws-sdk-net/issues/1569
                await subscribeQueueLimiter.WaitAsync().ConfigureAwait(false);

                // SNS dedups subscriptions based on the endpoint name
                // only the overload that takes the sqs client properly works with raw mode
                Logger.Debug($"Creating subscription for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
                var createdSubscription = await snsClient.SubscribeQueueAsync(topicArn, sqsClient, queueUrl).ConfigureAwait(false);
                Logger.Debug($"Created subscription with arn '{createdSubscription}' for '{topicName}' with arn '{topicArn}' for queue '{queueName}");

                Logger.Debug($"Setting raw delivery for subscription with arn '{createdSubscription}' for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
                await snsClient.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest
                {
                    SubscriptionArn = createdSubscription,
                    AttributeName = "RawMessageDelivery",
                    AttributeValue = "true"
                }).ConfigureAwait(false);
                Logger.Debug($"Set raw delivery for subscription with arn '{createdSubscription}' for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
            }
            finally
            {
                subscribeQueueLimiter.Release();
            }

            Logger.Debug($"Created subscription for queue '{queueName}' to topic '{topicName}' with arn '{topicArn}'");
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
        static readonly SemaphoreSlim subscribeQueueLimiter = new SemaphoreSlim(1);

        static ILog Logger = LogManager.GetLogger(typeof(SubscriptionManager));
    }
}