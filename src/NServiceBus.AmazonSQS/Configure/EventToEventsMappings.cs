namespace NServiceBus.Transport.AmazonSQS.Configure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class EventToEventsMappings
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

        public IEnumerable<Type> GetMappedTypes(Type eventType)
        {
            return eventsToEventsMappings.ContainsKey(eventType) ? eventsToEventsMappings[eventType] : Enumerable.Empty<Type>();
        }

        Dictionary<Type, HashSet<Type>> eventsToEventsMappings = new Dictionary<Type, HashSet<Type>>();
    }
}