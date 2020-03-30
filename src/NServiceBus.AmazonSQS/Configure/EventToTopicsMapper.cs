namespace NServiceBus
{
    using System;

    /// <summary>
    /// The event to topics mapper used to configure custom mapping between a message type
    /// and one or more topics in SNS.
    /// </summary>
    public class EventToTopicsMapper
    {
        internal EventToTopicsMapper( Type eventType, EventToTopicsMappings mappings)
        {
            this.eventType = eventType;
            this.mappings = mappings;
        }

        /// <summary>
        /// The list of topics names to map to. Topics will be created if non-existent.
        /// </summary>
        public void ToTopics(params string[] topicsNames)
        {
            Guard.AgainstNull(nameof(topicsNames), topicsNames);
            mappings.Add(eventType, topicsNames);
        }

        readonly Type eventType;
        readonly EventToTopicsMappings mappings;
    }
}