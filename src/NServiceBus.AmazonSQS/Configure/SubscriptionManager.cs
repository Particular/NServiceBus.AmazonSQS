namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
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
        public SubscriptionManager(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueUrlCache queueUrlCache, TransportConfiguration configuration, MessageMetadataRegistry messageMetadataRegistry)
        {
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.configuration = configuration;
            this.queueUrlCache = queueUrlCache;
            this.sqsClient = sqsClient;
            this.snsClient = snsClient;
            this.queueName = queueName;
        }

        public async Task Subscribe(Type eventType, ContextBag context)
        {
            var queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(queueName, configuration))
                .ConfigureAwait(false);

            await SetupTypeSubscriptions(eventType, queueUrl).ConfigureAwait(false);
        }

        public async Task Unsubscribe(Type eventType, ContextBag context)
        {
            var mostConcreteEventType = messageMetadataRegistry.GetMessageMetadata(eventType).MessageHierarchy[0];
            // not checking the topology cache
            if (mostConcreteEventType == typeof(object))
            {
                return;
            }

            await DeleteSubscription(TopicNameHelper.GetSnsTopicName(mostConcreteEventType.FullName, configuration)).ConfigureAwait(false);

            MarkTypeConfigured(eventType);
        }

        async Task DeleteSubscription(string topicName)
        {
            var existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            if (existingTopic == null)
            {
                return;
            }

            // TODO: Turn this into a while loop with next token
            var upToAHundredSubscriptions = await snsClient.ListSubscriptionsByTopicAsync(existingTopic.TopicArn).ConfigureAwait(false);
            var sqsQueueName = QueueNameHelper.GetSqsQueueName(queueName, configuration);
            foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
            {
                // TODO: Make this a bit better, not use linq and allocate the array all the time to not make me hate myself
                var last = upToAHundredSubscription.Endpoint.Split(new[] {":"}, StringSplitOptions.RemoveEmptyEntries).Last();
                if (string.Equals(sqsQueueName, last, StringComparison.Ordinal))
                {
                    await snsClient.UnsubscribeAsync(upToAHundredSubscription.SubscriptionArn).ConfigureAwait(false);
                    break;
                }
            }
        }

        async Task SetupTypeSubscriptions(Type eventType, string queueUrl)
        {
            var mostConcreteEventType = messageMetadataRegistry.GetMessageMetadata(eventType).MessageHierarchy[0];
            if (mostConcreteEventType == typeof(object) || IsTypeTopologyKnownConfigured(mostConcreteEventType))
            {
                return;
            }

            await CreateTopicAndSubscribe(TopicNameHelper.GetSnsTopicName(mostConcreteEventType.FullName, configuration), queueUrl).ConfigureAwait(false);

            MarkTypeConfigured(eventType);
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

        bool IsTypeTopologyKnownConfigured(Type eventType) => typeTopologyConfiguredSet.ContainsKey(eventType);

        readonly ConcurrentDictionary<Type, string> typeTopologyConfiguredSet = new ConcurrentDictionary<Type, string>();

        TransportConfiguration configuration;
        QueueUrlCache queueUrlCache;
        IAmazonSQS sqsClient;
        IAmazonSimpleNotificationService snsClient;
        string queueName;
        MessageMetadataRegistry messageMetadataRegistry;
        
        static ILog Logger = LogManager.GetLogger(typeof(SubscriptionManager));
    }
}