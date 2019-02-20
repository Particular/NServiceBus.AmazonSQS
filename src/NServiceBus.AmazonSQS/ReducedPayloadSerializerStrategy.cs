namespace NServiceBus.AmazonSQS
{
    using SimpleJson;

    class ReducedPayloadSerializerStrategy : PocoJsonSerializerStrategy
    {
        ReducedPayloadSerializerStrategy()
        {
            var cache = GetCache[typeof(TransportMessage)];
            cache.Remove(nameof(TransportMessage.TimeToBeReceived));
            cache.Remove(nameof(TransportMessage.ReplyToAddress));
        }

        static ReducedPayloadSerializerStrategy reducedPayloadSerializerStrategy;
        public static ReducedPayloadSerializerStrategy Instance => reducedPayloadSerializerStrategy ?? (reducedPayloadSerializerStrategy = new ReducedPayloadSerializerStrategy());
    }
}
