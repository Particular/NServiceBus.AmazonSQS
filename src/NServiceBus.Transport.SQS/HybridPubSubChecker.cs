namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Configure;
    using NServiceBus.Logging;
    using NServiceBus.Transport.SQS.Extensions;
    using Settings;

    class HybridPubSubChecker
    {
        sealed class SubscriptionsCacheItem
        {
            public bool IsThereAnSnsSubscription { get; set; }
            public DateTime Age { get; } = DateTime.UtcNow;
        }

        public HybridPubSubChecker(IReadOnlySettings settings, TopicCache topicCache, QueueCache queueCache, IAmazonSimpleNotificationService snsClient)
        {
            this.topicCache = topicCache;
            this.queueCache = queueCache;
            this.snsClient = snsClient;
            this.cacheTTL = TimeSpan.FromSeconds(5);

            if (settings != null && settings.TryGet(SettingsKeys.SubscriptionsCacheTTL, out TimeSpan cacheTTL))
            {
                this.cacheTTL = cacheTTL;
            }
        }

        public async Task<bool> ThisIsAPublishMessageNotUsingMessageDrivenPubSub(UnicastTransportOperation unicastTransportOperation, Dictionary<string, Type> multicastEventsMessageIdsToType, CancellationToken cancellationToken = default)
        {
            // The following check is required by the message-driven pub/sub hybrid mode in Core
            // to allow endpoints to migrate from message-driven pub/sub to native pub/sub
            // If the message we're trying to dispatch is a unicast message with a `Publish` intent
            // but the subscriber is also subscribed via SNS we don't want to dispatch the message twice
            // the subscriber will receive it via SNS and not via a unicast send.
            // We can improve the situation a bit by caching the information and thus reduce the amount of times we hit the SNS API.
            // We need to think abut what happens in case the destination endpoint unsubscribes from the event.
            // these conditions are carefully chosen to only execute the code if really necessary
            if (unicastTransportOperation != null
                && multicastEventsMessageIdsToType.ContainsKey(unicastTransportOperation.Message.MessageId)
                && unicastTransportOperation.Message.GetMessageIntent() == MessageIntent.Publish)
            {
                var eventType = multicastEventsMessageIdsToType[unicastTransportOperation.Message.MessageId];
                var existingTopic = await topicCache.GetTopic(eventType, cancellationToken).ConfigureAwait(false);
                if (existingTopic == null)
                {
                    return false;
                }

                var cacheKey = existingTopic.TopicArn + unicastTransportOperation.Destination;
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Performing subscription cache lookup for '{cacheKey}'.");
                }

                var lazyCacheItem = subscriptionsCache.AddOrUpdate(cacheKey,
                    static (cacheKey, state) =>
                    {
                        var (@this, topic, destination, cancellationToken) = state;
                        return @this.CreateLazyCacheItem(cacheKey, topic, destination, cancellationToken);
                    },
                    static (cacheKey, existingLazyCacheItem, state) =>
                    {
                        var (@this, topic, destination, cancellationToken) = state;
                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug($"Subscription found in cache, key: '{cacheKey}'.");
                        }

                        // if nothing has been materialized yet it is safe to return the existing entry because it will be fresh
                        if (!existingLazyCacheItem.IsValueCreated)
                        {
                            return existingLazyCacheItem;
                        }

                        // since the value is created there is nothing to await and thus it is safe to synchronously access the value
                        var subscriptionsCacheItem = existingLazyCacheItem.Value.GetAwaiter().GetResult();
                        if (subscriptionsCacheItem.Age.Add(@this.cacheTTL) < DateTime.UtcNow)
                        {
                            if (Logger.IsDebugEnabled)
                            {
                                Logger.Debug($"Removing subscription '{cacheKey}' from cache: TTL expired.");
                            }
                            return @this.CreateLazyCacheItem(cacheKey, topic, destination, cancellationToken);
                        }

                        return existingLazyCacheItem;
                    }, (this, existingTopic, unicastTransportOperation.Destination, cancellationToken));

                var cacheItem = await lazyCacheItem.Value.ConfigureAwait(false);

                if (cacheItem.IsThereAnSnsSubscription)
                {
                    return true;
                }
            }

            return false;
        }

        // Deliberately uses a task instead of value task because tasks can be awaited multiple times while value tasks should not be
        Lazy<Task<SubscriptionsCacheItem>> CreateLazyCacheItem(string cacheKey, Topic topic, string destination, CancellationToken cancellationToken) =>
            new(async () =>
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Subscription not found in cache, key: '{cacheKey}'.");
                    Logger.Debug($"Finding matching subscription for key '{cacheKey}' using SNS API.");
                }

                var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, topic, destination, cancellationToken)
                    .ConfigureAwait(false);

                var cacheItem = new SubscriptionsCacheItem { IsThereAnSnsSubscription = matchingSubscriptionArn != null };

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding subscription to cache as '{(cacheItem.IsThereAnSnsSubscription ? "found" : "not found")}', key: '{cacheKey}'.");
                }

                return cacheItem;
            }, LazyThreadSafetyMode.ExecutionAndPublication);

        readonly TimeSpan cacheTTL;
        readonly ConcurrentDictionary<string, Lazy<Task<SubscriptionsCacheItem>>> subscriptionsCache = new();
        static ILog Logger = LogManager.GetLogger(typeof(HybridPubSubChecker));
        readonly TopicCache topicCache;
        readonly QueueCache queueCache;
        readonly IAmazonSimpleNotificationService snsClient;
    }
}