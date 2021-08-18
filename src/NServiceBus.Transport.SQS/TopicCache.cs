namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Configure;
    using Unicast.Messages;

    class TopicCache
    {
        public TopicCache(IAmazonSimpleNotificationService snsClient, RateLimiter snsListTopicsRateLimiter, MessageMetadataRegistry messageMetadataRegistry, TransportConfiguration configuration)
        {
            this.configuration = configuration;
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.snsClient = snsClient;
            this.snsListTopicsRateLimiter = snsListTopicsRateLimiter;
            CustomEventToTopicsMappings = configuration.CustomEventToTopicsMappings ?? new EventToTopicsMappings();
            CustomEventToEventsMappings = configuration.CustomEventToEventsMappings ?? new EventToEventsMappings();
        }

        public EventToEventsMappings CustomEventToEventsMappings { get; }

        public EventToTopicsMappings CustomEventToTopicsMappings { get; }

        public Task<string> GetTopicArn(MessageMetadata metadata)
        {
            return GetAndCacheTopicIfFound(metadata).ContinueWith(t => t.Result?.TopicArn);
        }

        public Task<Topic> GetTopic(MessageMetadata metadata)
        {
            return GetAndCacheTopicIfFound(metadata);
        }

        public Task<string> GetTopicArn(Type eventType)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(eventType);
            return GetAndCacheTopicIfFound(metadata).ContinueWith(t => t.Result?.TopicArn);
        }

        public Task<string> GetTopicArn(string messageTypeIdentifier)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(messageTypeIdentifier);
            return GetAndCacheTopicIfFound(metadata).ContinueWith(t => t.Result?.TopicArn);
        }

        public Task<Topic> GetTopic(string messageTypeIdentifier)
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

        async Task<Topic> GetAndCacheTopicIfFound(MessageMetadata metadata)
        {
            if (topicCache.TryGetValue(metadata.MessageType, out var topic))
            {
                return topic;
            }

            var foundTopic = await snsListTopicsRateLimiter.Execute(async () =>
            {
                var topicName = GetTopicName(metadata);
                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return foundTopic != null ? topicCache.GetOrAdd(metadata.MessageType, foundTopic) : null;
        }

        IAmazonSimpleNotificationService snsClient;
        readonly RateLimiter snsListTopicsRateLimiter;
        MessageMetadataRegistry messageMetadataRegistry;
        TransportConfiguration configuration;
        ConcurrentDictionary<Type, Topic> topicCache = new ConcurrentDictionary<Type, Topic>();
        ConcurrentDictionary<Type, string> topicNameCache = new ConcurrentDictionary<Type, string>();
    }
}