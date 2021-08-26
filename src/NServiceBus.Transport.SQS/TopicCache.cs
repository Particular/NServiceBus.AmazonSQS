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

        bool TryGetTopicFromCache(MessageMetadata metadata, out Topic topic)
        {
            if (topicCache.TryGetValue(metadata.MessageType, out var topicCacheItem))
            {
                if (topicCacheItem.Topic == null && topicCacheItem.CreatedOn.Add(configuration.NotFoundTopicsCacheTTL) < DateTime.Now)
                {
                    Logger.Debug($"Removing topic '<null>' with key '{metadata.MessageType}' from cache: TTL expired.");
                    _ = topicCache.TryRemove(metadata.MessageType, out _);
                }
                else
                {
                    Logger.Debug($"Returning topic for '{metadata.MessageType}' from cache. Topic '{topicCacheItem.Topic?.TopicArn ?? "<null>"}'.");
                    topic = topicCacheItem.Topic;
                    return true;
                }
            }

            topic = null;
            return false;
        }

        async Task<Topic> GetAndCacheTopicIfFound(MessageMetadata metadata)
        {
            Logger.Debug($"Performing firt Topic cache lookup for '{metadata.MessageType}'.");
            if (TryGetTopicFromCache(metadata, out var cachedTopic))
            {
                return cachedTopic;
            }

            Logger.Debug($"Topic for '{metadata.MessageType}' not found in cache.");

            var foundTopic = await configuration.SnsListTopicsRateLimiter.Execute(async () =>
            {
                /*
                 * Rate limiter serializes requests, only 1 thread is allowed per
                 * rate limiter. Before trying to reach out to SNS we do another
                 * cache lookup
                 */
                Logger.Debug($"Performing second Topic cache lookup for '{metadata.MessageType}'.");
                if (TryGetTopicFromCache(metadata, out var cachedValue))
                {
                    return cachedValue;
                }

                var topicName = GetTopicName(metadata);
                Logger.Debug($"Finding topic '{topicName}' using 'ListTopics' SNS API.");

                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }).ConfigureAwait(false);

            //We cache also null/not found topics, they'll be wiped
            //from the cache at lookup time based on the configured TTL
            var added = topicCache.TryAdd(metadata.MessageType, new TopicCacheItem() { Topic = foundTopic });
            if (added)
            {
                Logger.Debug($"Added topic '{foundTopic?.TopicArn ?? "<null>"}' to cache. Cache items count: {topicCache.Count}.");
            }
            else
            {
                Logger.Debug($"Topic already present in cache. Topic '{foundTopic?.TopicArn ?? "<null>"}'. Cache items count: {topicCache.Count}.");
            }

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