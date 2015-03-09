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
		
            if (!config.GetSettings().UseSqsDeferral())
            {
                config.EnableFeature<TimeoutManagerBasedDeferral>();
                config.GetSettings().EnableFeatureByDefault<TimeoutManager>();
            }

            config.GetSettings().EnableFeatureByDefault<StorageDrivenPublishing>();
		
			//enable the outbox unless the users hasn't disabled it
			if (config.GetSettings().GetOrDefault<bool>(typeof(Features.Outbox).FullName))
			{
				config.EnableOutbox();
			}
		}
    }
}
