namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Configure;
    using NServiceBus.Logging;
    using NServiceBus.Transport.SQS.Extensions;
    using Settings;

    class HybridPubSubChecker
    {
        class SubscriptionsCacheItem
        {
            public bool IsThereAnSnsSubscription { get; set; }
            public DateTime Age { get; } = DateTime.UtcNow;
        }

        public HybridPubSubChecker(IReadOnlySettings settings)
        {
            rateLimiter = new SnsListSubscriptionsByTopicRateLimiter();

            this.cacheTTL = TimeSpan.FromSeconds(5);

            if (settings != null && settings.TryGet(SettingsKeys.SubscriptionsCacheTTL, out TimeSpan cacheTTL))
            {
                this.cacheTTL = cacheTTL;
            }
        }

        bool TryGetFromCache(string cacheKey, out SubscriptionsCacheItem item)
        {
            item = null;
            if (subscriptionsCache.TryGetValue(cacheKey, out var cacheItem))
            {
                Logger.Debug($"Subscription found in cache, key: '{cacheKey}'.");
                if (cacheItem.Age.Add(cacheTTL) < DateTime.UtcNow)
                {
                    Logger.Debug($"Removing subscription '{cacheKey}' from cache: TTL expired.");
                    subscriptionsCache.TryRemove(cacheKey, out _);
                }
                else
                {
                    item = cacheItem;
                }
            }
            else
            {
                Logger.Debug($"Subscription not found in cache, key: '{cacheKey}'.");
            }

            return item != null;
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async Task<bool> PublishUsingMessageDrivenPubSub(UnicastTransportOperation unicastTransportOperation, Dictionary<string, Type> multicastEventsMessageIdsToType, TopicCache topicCache, QueueCache queueCache, IAmazonSimpleNotificationService snsClient)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
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
                var existingTopic = await topicCache.GetTopic(eventType).ConfigureAwait(false);
                if (existingTopic == null)
                {
                    return true;
                }

                var cacheKey = existingTopic.TopicArn + unicastTransportOperation.Destination;
                Logger.Debug($"Performing first subscription cache lookup for '{cacheKey}'.");
                if (!TryGetFromCache(cacheKey, out var cacheItem))
                {
                    cacheItem = await rateLimiter.Execute(async () =>
                    {
                        Logger.Debug($"Performing second subscription cache lookup for '{cacheKey}'.");
                        if (TryGetFromCache(cacheKey, out var secondAttemptItem))
                        {
                            return secondAttemptItem;
                        }

                        Logger.Debug($"Finding matching subscription for key '{cacheKey}' using SNS API.");
                        var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, existingTopic, unicastTransportOperation.Destination)
                            .ConfigureAwait(false);

                        return new SubscriptionsCacheItem { IsThereAnSnsSubscription = matchingSubscriptionArn != null };
                    }).ConfigureAwait(false);

                    Logger.Debug($"Adding subscription to cache as '{(cacheItem.IsThereAnSnsSubscription ? "found" : "not found")}', key: '{cacheKey}'.");
                    _ = subscriptionsCache.TryAdd(cacheKey, cacheItem);
                }

                if (cacheItem.IsThereAnSnsSubscription)
                {
                    return false;
                }
            }

            return true;
        }

        SnsListSubscriptionsByTopicRateLimiter rateLimiter;
        readonly TimeSpan cacheTTL;
        readonly ConcurrentDictionary<string, SubscriptionsCacheItem> subscriptionsCache = new ConcurrentDictionary<string, SubscriptionsCacheItem>();
        static ILog Logger = LogManager.GetLogger(typeof(HybridPubSubChecker));
    }
}