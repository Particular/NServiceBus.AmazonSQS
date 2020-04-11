namespace NServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.Auth.AccessControlPolicy;
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
            var sqsQueueArn = await SetNecessaryDeliveryPolicies(topicArn, topicName, queueUrl).ConfigureAwait(false);
            var createdSubscription = await SubscribeQueue(topicArn, topicName, sqsQueueArn).ConfigureAwait(false);
            await SetRawDeliveryModeWithRetries(createdSubscription.SubscriptionArn, topicArn, topicName).ConfigureAwait(false);
            Logger.Debug($"Created subscription for queue '{queueName}' to topic '{topicName}' with arn '{topicArn}'");
        }

        async Task<SubscribeResponse> SubscribeQueue(string topicArn, string topicName, string sqsQueueArn)
        {
            // SNS dedups subscriptions based on the endpoint name
            Logger.Debug($"Creating subscription for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
            var createdSubscription = await snsClient.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = topicArn,
                Protocol = "sqs",
                Endpoint = sqsQueueArn
            }).ConfigureAwait(false);
            Logger.Debug($"Created subscription with arn '{createdSubscription.SubscriptionArn}' for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
            return createdSubscription;
        }

        async Task<string> SetNecessaryDeliveryPolicies(string topicArn, string topicName, string queueUrl)
        {
            Logger.Debug($"Setting delivery policies on queue '{queueName} for '{topicName}' with arn '{topicArn}'");
            string sqsQueueArn = null;
            for (var i = 0; i < 10; i++)
            {
                if (i > 1)
                {
                    var millisecondsDelay = i * 1000;
                    Logger.Debug($"Policy not yet propagated to enable topic '{topicName} with arn '{topicArn}' to write to '{sqsQueueArn}'! Retrying in {millisecondsDelay} ms.");
                    await Task.Delay(millisecondsDelay).ConfigureAwait(false);
                }

                var queueAttributes = await sqsClient.GetAttributesAsync(queueUrl).ConfigureAwait(false);
                sqsQueueArn = queueAttributes["QueueArn"];

                var policy = ExtractPolicy(queueAttributes);

                if (!policy.HasSQSPermission(topicArn, sqsQueueArn))
                {
                    policy.AddSQSPermission(topicArn, sqsQueueArn);
                }
                else
                {
                    break;
                }

                var setAttributes = new Dictionary<string, string> {{"Policy", policy.ToJson()}};
                await sqsClient.SetAttributesAsync(queueUrl, setAttributes).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(sqsQueueArn))
            {
                throw new Exception($"Unable to setup necessary policies for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
            }

            Logger.Debug($"Set delivery policies on queue '{queueName} for '{topicName}' with arn '{topicArn}'");
            return sqsQueueArn;
        }

        async Task SetRawDeliveryModeWithRetries(string subscriptionArn, string topicArn, string topicName)
        {
            Logger.Debug($"Setting raw delivery for subscription with arn '{subscriptionArn}' for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
            NotFoundException notFoundException;
            var iterationCount = 0;
            do
            {
                try
                {
                    await snsClient.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest
                    {
                        SubscriptionArn = subscriptionArn,
                        AttributeName = "RawMessageDelivery",
                        AttributeValue = "true"
                    }).ConfigureAwait(false);
                    notFoundException = null;
                }
                catch (NotFoundException exception) when (iterationCount < 5)
                {
                    notFoundException = exception;
                    iterationCount++;
                    var millisecondsDelay = iterationCount * 1000;
                    Logger.Debug($"Unable to set raw delivery mode for subscription with arn '{subscriptionArn}'! Retrying in {millisecondsDelay} ms.");
                    await Task.Delay(millisecondsDelay).ConfigureAwait(false);
                }
            } while (notFoundException != null);

            Logger.Debug($"Set raw delivery for subscription with arn '{subscriptionArn}' for '{topicName}' with arn '{topicArn}' for queue '{queueName}");
        }

        static Policy ExtractPolicy(Dictionary<string, string> queueAttributes)
        {
            Policy policy;
            string policyStr = null;
            if (queueAttributes.ContainsKey("Policy"))
            {
                policyStr = queueAttributes["Policy"];
            }

            if (string.IsNullOrEmpty(policyStr))
            {
                policy = new Policy();
            }
            else
            {
                policy = Policy.FromJson(policyStr);
            }

            return policy;
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

        static ILog Logger = LogManager.GetLogger(typeof(SubscriptionManager));
    }
}