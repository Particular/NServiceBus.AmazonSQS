namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Extensibility;
    using Extensions;
    using Logging;
    using Unicast.Messages;

    class SubscriptionManager : ISubscriptionManager
    {
        public SubscriptionManager(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueCache queueCache, TopicCache topicCache, PolicySettings policySettings, string topicNamePrefix)
        {
            this.topicCache = topicCache;
            this.policySettings = policySettings;
            this.topicNamePrefix = topicNamePrefix;
            this.queueCache = queueCache;
            this.sqsClient = sqsClient;
            this.snsClient = snsClient;
            this.queueName = queueName;
        }

        public async Task SubscribeAll(MessageMetadata[] eventTypes, ContextBag context, CancellationToken cancellationToken)
        {
            var queueUrl = await queueCache.GetQueueUrl(queueName)
                .ConfigureAwait(false);

            // currently we are not doing fanout but better safe than sorry later
            var policyStatementsToBeSettled = new ConcurrentBag<PolicyStatement>();

            foreach (var eventType in eventTypes)
            {
                //TODO: Can we do this concurrently?
                await SetupTypeSubscriptions(eventType, queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
            }
            await SettlePolicy(queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
        }

        public async Task Unsubscribe(MessageMetadata message, ContextBag context, CancellationToken cancellationToken)
        {
            await DeleteSubscription(message).ConfigureAwait(false);
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
                var mappedTypeMatchingSubscription = await snsClient.FindMatchingSubscription(queueCache, topicCache, mappedType, queueName)
                    .ConfigureAwait(false);
                if (mappedTypeMatchingSubscription != null)
                {
                    Logger.Debug($"Removing subscription with arn '{mappedTypeMatchingSubscription}' for queue '{queueName}'");
                    await snsClient.UnsubscribeAsync(mappedTypeMatchingSubscription).ConfigureAwait(false);
                    Logger.Debug($"Removed subscription with arn '{mappedTypeMatchingSubscription}' for queue '{queueName}'");
                }
            }

            var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, topicCache, metadata.MessageType, queueName)
                .ConfigureAwait(false);
            if (matchingSubscriptionArn != null)
            {
                Logger.Debug($"Removing subscription with arn '{matchingSubscriptionArn}' for queue '{queueName}'");
                await snsClient.UnsubscribeAsync(matchingSubscriptionArn).ConfigureAwait(false);
                Logger.Debug($"Removed subscription with arn '{matchingSubscriptionArn}' for queue '{queueName}'");
            }

            MarkTypeNotConfigured(metadata.MessageType);
        }

        async Task SetupTypeSubscriptions(MessageMetadata metadata, string queueUrl, ConcurrentBag<PolicyStatement> policyStatementsToBeSettled)
        {
            var mappedTopicsNames = topicCache.CustomEventToTopicsMappings.GetMappedTopicsNames(metadata.MessageType);
            foreach (var mappedTopicName in mappedTopicsNames)
            {
                //we skip the topic name generation assuming the topic name is already good
                Logger.Debug($"Creating topic/subscription to '{mappedTopicName}' for queue '{queueName}'");
                await CreateTopicAndSubscribe(mappedTopicName, queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
                Logger.Debug($"Created topic/subscription to '{mappedTopicName}' for queue '{queueName}'");
            }

            var mappedTypes = topicCache.CustomEventToEventsMappings.GetMappedTypes(metadata.MessageType);
            foreach (var mappedType in mappedTypes)
            {
                // doesn't need to be cached since we never publish to it
                Logger.Debug($"Creating topic/subscription for '{mappedType.FullName}' for queue '{queueName}'");
                await CreateTopicAndSubscribe(mappedType, queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
                Logger.Debug($"Created topic/subscription for '{mappedType.FullName}' for queue '{queueName}'");
            }

            if (IsTypeTopologyKnownConfigured(metadata.MessageType))
            {
                Logger.Debug($"Skipped subscription for '{metadata.MessageType.FullName}' for queue '{queueName}' because it is already configured");
                return;
            }

            Logger.Debug($"Creating topic/subscription for '{metadata.MessageType.FullName}' for queue '{queueName}'");
            await CreateTopicAndSubscribe(metadata.MessageType, queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
            Logger.Debug($"Created topic/subscription for '{metadata.MessageType.FullName}' for queue '{queueName}'");
            MarkTypeConfigured(metadata.MessageType);
        }

        async Task CreateTopicAndSubscribe(string topicName, string queueUrl, ConcurrentBag<PolicyStatement> policyStatementsToBeSettled)
        {
            Logger.Debug($"Getting or creating topic '{topicName}' for queue '{queueName}");
            var createTopicResponse = await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
            Logger.Debug($"Got or created topic '{topicName}' with arn '{createTopicResponse.TopicArn}' for queue '{queueName}");

            await SubscribeTo(createTopicResponse.TopicArn, topicName, queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
        }

        async Task CreateTopicAndSubscribe(Type eventType, string queueUrl, ConcurrentBag<PolicyStatement> policyStatementsToBeSettled)
        {
            var topicName = topicCache.GetTopicName(eventType);
            Logger.Debug($"Getting or creating topic '{topicName}' for queue '{queueName}");
            var createTopicResponse = await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
            Logger.Debug($"Got or created topic '{topicName}' with arn '{createTopicResponse.TopicArn}' for queue '{queueName}");

            await SubscribeTo(createTopicResponse.TopicArn, topicName, queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
        }

        async Task SubscribeTo(string topicArn, string topicName, string queueUrl, ConcurrentBag<PolicyStatement> policyStatementsToBeSettled)
        {
            var sqsQueueArn = await queueCache.GetQueueArn(queueUrl).ConfigureAwait(false);

            var addPolicyStatement = new PolicyStatement(topicName, topicArn, sqsQueueArn);

            policyStatementsToBeSettled.Add(addPolicyStatement);
        }

        async Task SettlePolicy(string queueUrl, IReadOnlyCollection<PolicyStatement> policyStatementsToBeSettled)
        {
            Logger.Debug($"Settling policy for queue '{queueName}'.");

            await SetNecessaryDeliveryPolicyWithRetries(queueUrl, policyStatementsToBeSettled).ConfigureAwait(false);
            foreach (var addPolicyStatement in policyStatementsToBeSettled)
            {
                await SubscribeQueueToTopic(addPolicyStatement).ConfigureAwait(false);
            }

            Logger.Debug($"Settled policy for queue '{queueName}'.");
        }

        async Task SubscribeQueueToTopic(PolicyStatement policyStatement)
        {
            Logger.Debug($"Creating subscription for queue '{queueName}' to topic '{policyStatement.TopicName}' with arn '{policyStatement.TopicArn}'");

            await SubscribeQueue(policyStatement).ConfigureAwait(false);

            Logger.Debug($"Created subscription for queue '{queueName}' to topic '{policyStatement.TopicName}' with arn '{policyStatement.TopicArn}'");
        }

        async Task SubscribeQueue(PolicyStatement policyStatement)
        {
            // SNS deduplicates subscriptions based on the endpoint name
            Logger.Debug($"Creating subscription for '{policyStatement.TopicName}' with arn '{policyStatement.TopicArn}' for queue '{queueName}");
            var createdSubscription = await snsClient.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = policyStatement.TopicArn,
                Protocol = "sqs",
                Endpoint = policyStatement.QueueArn,
                ReturnSubscriptionArn = true,
                Attributes = new Dictionary<string, string>
                {
                    { "RawMessageDelivery", "true" }
                }
            }).ConfigureAwait(false);
            Logger.Debug($"Created subscription with arn '{createdSubscription.SubscriptionArn}' for '{policyStatement.TopicName}' with arn '{policyStatement.TopicArn}' for queue '{queueName}");
        }

        async Task SetNecessaryDeliveryPolicyWithRetries(string queueUrl, IReadOnlyCollection<PolicyStatement> addPolicyStatements)
        {

            if (!policySettings.SetupTopicPoliciesWhenSubscribing || addPolicyStatements.Count == 0)
            {
                return;
            }

            try
            {
                // need to safe guard the subscribe section so that policy are not overwritten
                // deliberately not set a cancellation token for now
                // https://github.com/aws/aws-sdk-net/issues/1569
                await subscribeQueueLimiter.WaitAsync().ConfigureAwait(false);

                Logger.Debug($"Setting delivery policies on queue '{queueName}.");
                string sqsQueueArn = null;
                for (var i = 0; i < 10; i++)
                {
                    if (i > 1)
                    {
                        var millisecondsDelay = i * 1000;
                        Logger.Debug(
                            $"Policy not yet propagated to write to '{sqsQueueArn}'! Retrying in {millisecondsDelay} ms.");
                        await Delay(millisecondsDelay).ConfigureAwait(false);
                    }

                    var queueAttributes = await sqsClient.GetAttributesAsync(queueUrl).ConfigureAwait(false);
                    sqsQueueArn = queueAttributes["QueueArn"];

                    var policy = queueAttributes.ExtractPolicy();

                    if (!policy.Update(addPolicyStatements,
                        policySettings.AccountCondition,
                        policySettings.TopicNamePrefixCondition,
                        policySettings.TopicNamespaceConditions,
                        topicNamePrefix,
                        sqsQueueArn))
                    {
                        break;
                    }

                    var setAttributes = new Dictionary<string, string> { { "Policy", policy.ToJson() } };
                    await sqsClient.SetAttributesAsync(queueUrl, setAttributes).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(sqsQueueArn))
                {
                    throw new Exception($"Unable to setup necessary policies for queue '{queueName}");
                }

                Logger.Debug($"Set delivery policies on queue '{queueName}");
            }
            finally
            {
                subscribeQueueLimiter.Release();
            }
        }

        // only for testing
        protected virtual Task Delay(int millisecondsDelay, CancellationToken token = default)
        {
            return Task.Delay(millisecondsDelay, token);
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
        readonly TopicCache topicCache;
        readonly PolicySettings policySettings;
        readonly string topicNamePrefix;
        readonly SemaphoreSlim subscribeQueueLimiter = new SemaphoreSlim(1);

        static readonly ILog Logger = LogManager.GetLogger(typeof(SubscriptionManager));
    }
}
