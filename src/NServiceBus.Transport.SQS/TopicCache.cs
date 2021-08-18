namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Configure;
    using Unicast.Messages;

    class TopicCache
    {
        public TopicCache(IAmazonSimpleNotificationService snsClient, RateLimiter snsRequestsRateLimiter, MessageMetadataRegistry messageMetadataRegistry, TransportConfiguration configuration)
        {
            this.configuration = configuration;
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.snsClient = snsClient;
            this.snsRequestsRateLimiter = snsRequestsRateLimiter;
            CustomEventToTopicsMappings = configuration.CustomEventToTopicsMappings ?? new EventToTopicsMappings();
            CustomEventToEventsMappings = configuration.CustomEventToEventsMappings ?? new EventToEventsMappings();
        }

        public EventToEventsMappings CustomEventToEventsMappings { get; }

        public EventToTopicsMappings CustomEventToTopicsMappings { get; }

        public Task<string> GetTopicArn(MessageMetadata metadata)
        {
            return GetAndCacheTopicIfFound(metadata);
        }

        public Task<string> GetTopicArn(Type eventType)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(eventType);
            return GetAndCacheTopicIfFound(metadata);
        }

        public Task<string> GetTopicArn(string messageTypeIdentifier)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(messageTypeIdentifier);
            return GetAndCacheTopicIfFound(metadata);
        }

        public string GetTopicName(MessageMetadata metadata)
        {
            if (topicNameCache.TryGetValue(metadata.MessageType, out var topicName))
            {
                return topicName;
            }

            return topicNameCache.GetOrAdd(metadata.MessageType, configuration.TopicNameGenerator(metadata));
        }

        async Task<string> GetAndCacheTopicIfFound(MessageMetadata metadata)
        {
            if (topicCache.TryGetValue(metadata.MessageType, out var topic))
            {
                return topic;
            }

            var foundTopic = await snsRequestsRateLimiter.Execute(async () =>
            {
                var topicName = GetTopicName(metadata);
                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return foundTopic != null ? topicCache.GetOrAdd(metadata.MessageType, foundTopic.TopicArn) : null;
        }

        IAmazonSimpleNotificationService snsClient;
        readonly RateLimiter snsRequestsRateLimiter;
        MessageMetadataRegistry messageMetadataRegistry;
        TransportConfiguration configuration;
        ConcurrentDictionary<Type, string> topicCache = new ConcurrentDictionary<Type, string>();
        ConcurrentDictionary<Type, string> topicNameCache = new ConcurrentDictionary<Type, string>();
    }
}