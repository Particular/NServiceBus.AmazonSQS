namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
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
        public SubscriptionManager(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueCache queueCache, TransportConfiguration configuration, MessageMetadataRegistry messageMetadataRegistry)
        {
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.configuration = configuration;
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

            await DeleteSubscription(configuration.TopicNameGenerator(metadata.MessageType, configuration.TopicNamePrefix)).ConfigureAwait(false);

            MarkTypeNotConfigured(metadata.MessageType);
        }

        async Task DeleteSubscription(string topicName)
        {
            var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, topicName, queueName)
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

            await CreateTopicAndSubscribe(configuration.TopicNameGenerator(metadata.MessageType, configuration.TopicNamePrefix), queueUrl).ConfigureAwait(false);

            MarkTypeConfigured(metadata.MessageType);
        }

        async Task CreateTopicAndSubscribe(string topicName, string queueUrl)
        {
            var existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            if (existingTopic == null)
            {
                await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
                Logger.Debug($"Created topic '{topicName}'");
                existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }

            // SNS dedups subscriptions based on the endpoint name
            // only the overload that takes the sqs client properly works with raw mode

            var createdSubscription = await snsClient.SubscribeQueueAsync(existingTopic.TopicArn, sqsClient, queueUrl).ConfigureAwait(false);
            var setSubscriptionAttributesRequest = new SetSubscriptionAttributesRequest
            {
                SubscriptionArn = createdSubscription,
                AttributeName = "RawMessageDelivery",
                AttributeValue = "true"
            };
            await snsClient.SetSubscriptionAttributesAsync(setSubscriptionAttributesRequest).ConfigureAwait(false);

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

        TransportConfiguration configuration;
        QueueCache queueCache;
        IAmazonSQS sqsClient;
        IAmazonSimpleNotificationService snsClient;
        string queueName;
        MessageMetadataRegistry messageMetadataRegistry;

        static ILog Logger = LogManager.GetLogger(typeof(SubscriptionManager));
    }
}