namespace NServiceBus
{
    using System;
    using System.Collections.Generic;

    class EventToEventMappings
    {
        public void Add(Type subscribedEvent, Type concreteEventType)
        {
            if (!eventsToEventsMappings.TryGetValue(subscribedEvent, out var mapping))
            {
                mapping = new HashSet<Type>();
                eventsToEventsMappings.Add(subscribedEvent, mapping);
            }

            mapping.Add(concreteEventType);
        }

        public bool HasMappingsFor(Type eventType)
        {
            return eventsToEventsMappings.ContainsKey(eventType);
        }

        public IEnumerable<Type> GetMappedTypes(Type eventType)
        {
            return eventsToEventsMappings[eventType];
        }

        Dictionary<Type, HashSet<Type>> eventsToEventsMappings = new Dictionary<Type, HashSet<Type>>();
    }
}