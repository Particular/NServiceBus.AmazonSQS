namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
        
        public IEnumerable<string> GetMappedTopicsNames(Type eventType)
        {
            return eventsToTopicsMappings.ContainsKey(eventType) ? eventsToTopicsMappings[eventType] : Enumerable.Empty<string>();
        }

        Dictionary<Type, HashSet<string>> eventsToTopicsMappings = new Dictionary<Type, HashSet<string>>();
    }
}