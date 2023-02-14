#nullable enable
namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Configure;
    using Logging;
    using Settings;

    class TopicCache
    {
        class TopicCacheItem
        {
            public Topic? Topic { get; set; }

            public DateTime CreatedOn { get; } = DateTime.UtcNow;
        }

        public TopicCache(IAmazonSimpleNotificationService snsClient, IReadOnlySettings? settings, EventToTopicsMappings eventToTopicsMappings, EventToEventsMappings eventToEventsMappings, Func<Type, string, string> topicNameGenerator, string topicNamePrefix)
        {
            this.topicNameGenerator = topicNameGenerator;
            this.topicNamePrefix = topicNamePrefix;
            this.snsClient = snsClient;

            CustomEventToTopicsMappings = eventToTopicsMappings;
            CustomEventToEventsMappings = eventToEventsMappings;

            notFoundTopicsCacheTTL = TimeSpan.FromSeconds(5);

            if (settings != null && settings.TryGet(SettingsKeys.NotFoundTopicsCacheTTL, out TimeSpan ttl))
            {
                notFoundTopicsCacheTTL = ttl;
            }
        }

        public EventToEventsMappings CustomEventToEventsMappings { get; }

        public EventToTopicsMappings CustomEventToTopicsMappings { get; }

        public async ValueTask<string?> GetTopicArn(Type eventType, CancellationToken cancellationToken = default)
        {
            var topic = await GetAndCacheTopicIfFound(eventType, cancellationToken).ConfigureAwait(false);
            return topic?.TopicArn;
        }

        public string GetTopicName(Type messageType)
        {
            if (topicNameCache.TryGetValue(messageType, out var topicName))
            {
                return topicName;
            }

            return topicNameCache.GetOrAdd(messageType, topicNameGenerator(messageType, topicNamePrefix));
        }
        public ValueTask<Topic?> GetTopic(Type eventType, CancellationToken cancellationToken = default)
            => GetAndCacheTopicIfFound(eventType, cancellationToken);

        bool TryGetTopicFromCache(Type messageType, out Topic? topic)
        {
            if (topicCache.TryGetValue(messageType, out var topicCacheItem))
            {
                if (topicCacheItem.Topic == null && topicCacheItem.CreatedOn.Add(notFoundTopicsCacheTTL) < DateTime.UtcNow)
                {
                    Logger.Debug($"Removing topic '<null>' with key '{messageType}' from cache: TTL expired.");
                    _ = topicCache.TryRemove(messageType, out _);
                }
                else
                {
                    Logger.Debug($"Returning topic for '{messageType}' from cache. Topic '{topicCacheItem.Topic?.TopicArn ?? "<null>"}'.");
                    topic = topicCacheItem.Topic;
                    return true;
                }
            }

            topic = null;
            return false;
        }

        async ValueTask<Topic?> GetAndCacheTopicIfFound(Type messageType, CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Performing first Topic cache lookup for '{messageType}'.");
            }

            if (TryGetTopicFromCache(messageType, out var cachedTopic))
            {
                return cachedTopic;
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Topic for '{messageType}' not found in cache.");
            }

            var foundTopic = await snsListTopicsRateLimiter.Execute(static async (state, cancellationToken) =>
            {
                var (@this, messageType, snsClient) = state;
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Performing second Topic cache lookup for '{messageType}'.");
                }
                /*
                 * Rate limiter serializes requests, only 1 thread is allowed per
                 * rate limiter. Before trying to reach out to SNS we do another
                 * cache lookup
                 */
                if (@this.TryGetTopicFromCache(messageType, out var cachedValue))
                {
                    return cachedValue;
                }

                var topicName = @this.GetTopicName(messageType);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Finding topic '{topicName}' using 'ListTopics' SNS API.");
                }

                return await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            }, (this, messageType, snsClient), cancellationToken).ConfigureAwait(false);

            //We cache also null/not found topics, they'll be wiped
            //from the cache at lookup time based on the configured TTL
            var added = topicCache.TryAdd(messageType, new TopicCacheItem { Topic = foundTopic });
            if (added)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug(
                        $"Added topic '{foundTopic?.TopicArn ?? "<null>"}' to cache. Cache items count: {topicCache.Count}.");
                }
            }
            else
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug(
                        $"Topic already present in cache. Topic '{foundTopic?.TopicArn ?? "<null>"}'. Cache items count: {topicCache.Count}.");
                }
            }

            return foundTopic;
        }

        IAmazonSimpleNotificationService snsClient;
        ConcurrentDictionary<Type, TopicCacheItem> topicCache = new();
        ConcurrentDictionary<Type, string> topicNameCache = new();
        static ILog Logger = LogManager.GetLogger(typeof(TopicCache));
        readonly Func<Type, string, string> topicNameGenerator;
        readonly string topicNamePrefix;
        readonly TimeSpan notFoundTopicsCacheTTL;
        readonly SnsListTopicsRateLimiter snsListTopicsRateLimiter = new();
    }
}