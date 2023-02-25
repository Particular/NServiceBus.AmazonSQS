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
        sealed class TopicCacheItem
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

        async ValueTask<Topic?> GetAndCacheTopicIfFound(Type messageType, CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Performing topic cache lookup for '{messageType}'.");
            }

            var lazyCacheItem = topicCache.AddOrUpdate(messageType,
                static (type, @this) => @this.CreateLazyCacheItem(type),
                static (messageType, existingLazyCacheItem, @this) =>
            {
                // if nothing has been materialized yet it is safe to return the existing entry because it will be fresh
                if (!existingLazyCacheItem.IsValueCreated)
                {
                    return existingLazyCacheItem;
                }

                // since the value is created there is nothing to await and thus it is safe to synchronously access the value
                var topicCacheItem = existingLazyCacheItem.Value.GetAwaiter().GetResult();
                if (topicCacheItem.Topic == null && topicCacheItem.CreatedOn.Add(@this.notFoundTopicsCacheTTL) < DateTime.UtcNow)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Removing topic '<null>' with key '{messageType}' from cache: TTL expired.");
                    }
                    return @this.CreateLazyCacheItem(messageType);
                }

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Returning topic for '{messageType}' from cache. Topic '{topicCacheItem.Topic?.TopicArn ?? "<null>"}'.");
                }
                return existingLazyCacheItem;
            }, this);

            return (await lazyCacheItem.Value.ConfigureAwait(false)).Topic;
        }

        // Deliberately uses a task instead of value task because tasks can be awaited multiple times while value tasks should not be
        Lazy<Task<TopicCacheItem>> CreateLazyCacheItem(Type messageType) =>
            new(async () =>
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Topic for '{messageType}' not found in cache.");
                }

                var topicName = GetTopicName(messageType);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Finding topic '{topicName}' using 'ListTopics' SNS API.");
                }

                var topic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
                return new TopicCacheItem { Topic = topic };
            }, LazyThreadSafetyMode.ExecutionAndPublication);

        IAmazonSimpleNotificationService snsClient;
        ConcurrentDictionary<Type, Lazy<Task<TopicCacheItem>>> topicCache = new();
        ConcurrentDictionary<Type, string> topicNameCache = new();
        static ILog Logger = LogManager.GetLogger(typeof(TopicCache));
        readonly Func<Type, string, string> topicNameGenerator;
        readonly string topicNamePrefix;
        readonly TimeSpan notFoundTopicsCacheTTL;
    }
}