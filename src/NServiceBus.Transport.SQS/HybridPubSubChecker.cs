namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using NServiceBus.Transport.SQS.Extensions;

    class HybridPubSubChecker
    {
        class SubscritionCacheItem
        {
            public bool IsThereAnSnsSubscription { get; set; }
            public DateTime Age { get; } = DateTime.Now;
        }

        public async Task<bool> PublishUsingMessageDrivenPubSub(UnicastTransportOperation unicastTransportOperation, HashSet<string> messageIdsOfMulticastedEvents, TopicCache topicCache, QueueCache queueCache, IAmazonSimpleNotificationService snsClient)
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
                    return true;
                }

                var cacheKey = existingTopic.TopicArn + unicastTransportOperation.Destination;
                if (subscriptionsCache.ContainsKey(cacheKey))
                {
                    var cacheItem = subscriptionsCache[cacheKey];
                    if (cacheItem.Age.Add(cacheTTL) < DateTime.Now)
                    {
                        _ = subscriptionsCache.TryRemove(cacheKey, out _);
                    }
                }

                if (!subscriptionsCache.ContainsKey(cacheKey))
                {
                    var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, existingTopic, unicastTransportOperation.Destination)
                        .ConfigureAwait(false);

                    if (matchingSubscriptionArn != null)
                    {
                        _ = subscriptionsCache.TryAdd(cacheKey, new SubscritionCacheItem { IsThereAnSnsSubscription = true });
                    }
                    else
                    {
                        _ = subscriptionsCache.TryAdd(cacheKey, new SubscritionCacheItem { IsThereAnSnsSubscription = false });
                    }
                }

                var isThereAnSnsSubscription = subscriptionsCache[cacheKey].IsThereAnSnsSubscription;
                if (isThereAnSnsSubscription)
                {
                    return false;
                }
            }

            return true;
        }

        static readonly TimeSpan cacheTTL = TimeSpan.FromSeconds(60);
        readonly ConcurrentDictionary<string, SubscritionCacheItem> subscriptionsCache = new ConcurrentDictionary<string, SubscritionCacheItem>();
    }
}