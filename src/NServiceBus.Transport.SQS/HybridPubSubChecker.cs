namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using NServiceBus.Logging;
    using NServiceBus.Transport.SQS.Extensions;

    class HybridPubSubChecker
    {
        class SubscriptionsCacheItem
        {
            public bool IsThereAnSnsSubscription { get; set; }
            public DateTime Age { get; } = DateTime.Now;
        }

        public HybridPubSubChecker(TransportConfiguration configuration)
        {
            rateLimiter = configuration.SnsListSubscriptionsByTopicRateLimiter;
            cacheTTL = configuration.SubscriptionsCacheTTL;
        }

        bool TryGetFromCache(string cacheKey, out SubscriptionsCacheItem item)
        {
            item = null;
            if (subscriptionsCache.TryGetValue(cacheKey, out var cacheItem))
            {
                Logger.Debug($"Subscription found in cache, key: '{cacheKey}'.");
                if (cacheItem.Age.Add(cacheTTL) < DateTime.Now)
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

        public async Task<bool> ThisIsAPublishMessageNotUsingMessageDrivenPubSub(UnicastTransportOperation unicastTransportOperation, HashSet<string> messageIdsOfMulticastedEvents, TopicCache topicCache, QueueCache queueCache, IAmazonSimpleNotificationService snsClient)
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
                && messageIdsOfMulticastedEvents.Contains(unicastTransportOperation.Message.MessageId)
                && unicastTransportOperation.Message.GetMessageIntent() == MessageIntentEnum.Publish
                && unicastTransportOperation.Message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            {
                var mostConcreteEnclosedMessageType = unicastTransportOperation.Message.GetEnclosedMessageTypes()[0];
                var existingTopic = await topicCache.GetTopic(mostConcreteEnclosedMessageType).ConfigureAwait(false);
                if (existingTopic == null)
                {
                    return false;
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
                    return true;
                }
            }

            return false;
        }

        SnsListSubscriptionsByTopicRateLimiter rateLimiter;
        readonly TimeSpan cacheTTL;
        readonly ConcurrentDictionary<string, SubscriptionsCacheItem> subscriptionsCache = new ConcurrentDictionary<string, SubscriptionsCacheItem>();
        static ILog Logger = LogManager.GetLogger(typeof(HybridPubSubChecker));
    }
}