namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Configure;
    using NServiceBus.Logging;
    using Unicast.Messages;

    class TopicCache
    {
        class TopicCacheItem
        {
            public Topic Topic { get; set; }

            public DateTime CreatedOn { get; } = DateTime.Now;
        }

        public TopicCache(IAmazonSimpleNotificationService snsClient, MessageMetadataRegistry messageMetadataRegistry, TransportConfiguration configuration)
        {
            this.configuration = configuration;
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.snsClient = snsClient;
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
            if (topicCache.TryGetValue(metadata.MessageType, out var topicCacheItem))
            {
                if (topicCacheItem.Topic == null && topicCacheItem.CreatedOn.Add(configuration.NotFoundTopicsCacheTTL) < DateTime.Now)
                {
                    Logger.Debug($"Removing topic '<null>' with key '{metadata.MessageType}' from cache.");
                    _ = topicCache.TryRemove(metadata.MessageType, out _);
                }
                else
                {
                    return topicCacheItem.Topic;
                }
            }

            var foundTopic = await configuration.SnsListTopicsRateLimiter.Execute(async () =>
            {
                var topicName = GetTopicName(metadata);
                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Logger.Debug($"Adding topic '{foundTopic?.TopicArn ?? "<null>"}' to cache.");

            //We cache also null/not found topics, they'll be wiped
            //from the cache at lookup time based on the nullTopicCacheTTL
            topicCache.GetOrAdd(metadata.MessageType, new TopicCacheItem() { Topic = foundTopic });

            return foundTopic;
        }

        IAmazonSimpleNotificationService snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        TransportConfiguration configuration;
        ConcurrentDictionary<Type, TopicCacheItem> topicCache = new ConcurrentDictionary<Type, TopicCacheItem>();
        ConcurrentDictionary<Type, string> topicNameCache = new ConcurrentDictionary<Type, string>();
        static ILog Logger = LogManager.GetLogger(typeof(TopicCache));
    }
}