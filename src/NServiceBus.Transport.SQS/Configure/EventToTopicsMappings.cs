namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class EventToTopicsMappings
    {
        public void Add(Type subscribedEventType, IEnumerable<string> topicsNames)
        {
            if (!eventsToTopicsMappings.TryGetValue(subscribedEventType, out var mapping))
            {
                mapping = new HashSet<string>();
                eventsToTopicsMappings.Add(subscribedEventType, mapping);
            }

            foreach (var topicName in topicsNames)
            {
                mapping.Add(topicName);
            }
        }

        public IEnumerable<string> GetMappedTopicsNames(Type subscribedEventType)
        {
            return eventsToTopicsMappings.ContainsKey(subscribedEventType) ? eventsToTopicsMappings[subscribedEventType] : Enumerable.Empty<string>();
        }

        Dictionary<Type, HashSet<string>> eventsToTopicsMappings = new Dictionary<Type, HashSet<string>>();
    }
}