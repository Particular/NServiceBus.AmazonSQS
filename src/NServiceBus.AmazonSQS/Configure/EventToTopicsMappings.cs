namespace NServiceBus
{
    using System;
    using System.Collections.Generic;

    class EventToTopicsMappings
    {
        public void Add(Type eventType, IEnumerable<string> topicsNames)
        {
            if (!eventsToTopicsMappings.TryGetValue(eventType, out var mapping))
            {
                mapping = new HashSet<string>();
                eventsToTopicsMappings.Add(eventType, mapping);
            }

            foreach (var topicName in topicsNames)
            {
                mapping.Add(topicName);
            }
        }

        public bool HasMappingsFor(Type eventType)
        {
            return eventsToTopicsMappings.ContainsKey(eventType);
        }

        public IEnumerable<string> GetMappedTopicsNames(Type eventType)
        {
            return eventsToTopicsMappings[eventType];
        }

        Dictionary<Type, HashSet<string>> eventsToTopicsMappings = new Dictionary<Type, HashSet<string>>();
    }
}