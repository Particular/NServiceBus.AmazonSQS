namespace NServiceBus
{
	using Configuration.AdvanceExtensibility;
	using Features;
	using Transports;

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
			config.EnableFeature<TimeoutManagerBasedDeferral>();
			config.GetSettings().EnableFeatureByDefault<StorageDrivenPublishing>();
			config.GetSettings().EnableFeatureByDefault<TimeoutManager>();
		}
    }
}
