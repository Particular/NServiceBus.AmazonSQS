namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class EventToEventsMappings
    {
        public void Add(Type subscribedEventType, Type publishedEventType)
        {
            if (!eventsToEventsMappings.TryGetValue(subscribedEventType, out var mapping))
            {
                mapping = [];
                eventsToEventsMappings.Add(subscribedEventType, mapping);
            }

            mapping.Add(publishedEventType);
        }

        public IEnumerable<Type> GetMappedTypes(Type eventType)
        {
            return eventsToEventsMappings.ContainsKey(eventType) ? eventsToEventsMappings[eventType] : Enumerable.Empty<Type>();
        }

        Dictionary<Type, HashSet<Type>> eventsToEventsMappings = new Dictionary<Type, HashSet<Type>>();
    }
}