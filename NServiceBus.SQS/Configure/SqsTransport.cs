namespace NServiceBus
{
	using NServiceBus.Transports;

    public class SqsTransport : TransportDefinition
    {
        public SqsTransport()
        {
            HasNativePubSubSupport = false;
            HasSupportForCentralizedPubSub = false;
        }
    }
}
