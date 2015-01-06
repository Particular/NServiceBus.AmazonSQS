namespace NServiceBus
{
	using NServiceBus.Features;
	using NServiceBus.Transports;

    public class SqsTransport : TransportDefinition
    {
        public SqsTransport()
        {
            HasNativePubSubSupport = false;
            HasSupportForCentralizedPubSub = false;
			HasSupportForDistributedTransactions = false;
        }

		protected override void Configure(BusConfiguration config)
		{
			config.EnableFeature<SqsTransportFeature>();
			config.EnableFeature<MessageDrivenSubscriptions>();
		}
    }
}
