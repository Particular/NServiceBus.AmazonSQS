namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Configure;

    class TopicCache
    {
        public TopicCache(IAmazonSimpleNotificationService snsClient, EventToTopicsMappings eventToTopicsMappings, EventToEventsMappings eventToEventsMappings, Func<Type, string, string> topicNameGenerator, string topicNamePrefix)
        {
            this.topicNameGenerator = topicNameGenerator;
            this.topicNamePrefix = topicNamePrefix;
            this.snsClient = snsClient;
            CustomEventToTopicsMappings = eventToTopicsMappings;
            CustomEventToEventsMappings = eventToEventsMappings;
        }

        public EventToEventsMappings CustomEventToEventsMappings { get; }

        public EventToTopicsMappings CustomEventToTopicsMappings { get; }

        public Task<string> GetTopicArn(Type eventType)
        {
            return GetAndCacheTopicIfFound(eventType);
        }

        public string GetTopicName(Type evenType)
        {
            if (topicNameCache.TryGetValue(evenType, out var topicName))
            {
                return topicName;
            }

            return topicNameCache.GetOrAdd(evenType, topicNameGenerator(evenType, topicNamePrefix));
        }

        async Task<string> GetAndCacheTopicIfFound(Type evenType)
        {
            if (topicCache.TryGetValue(evenType, out var topic))
            {
                return topic;
            }

            var topicName = GetTopicName(evenType);
            var foundTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            return foundTopic != null ? topicCache.GetOrAdd(evenType, foundTopic.TopicArn) : null;
        }

        IAmazonSimpleNotificationService snsClient;
        readonly Func<Type, string, string> topicNameGenerator;
        readonly string topicNamePrefix;
        ConcurrentDictionary<Type, string> topicCache = new ConcurrentDictionary<Type, string>();
        ConcurrentDictionary<Type, string> topicNameCache = new ConcurrentDictionary<Type, string>();
    }
}