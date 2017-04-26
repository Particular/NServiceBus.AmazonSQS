namespace NServiceBus
{
    using System;
    using Configuration.AdvanceExtensibility;
    using Features;
    using Settings;
    using Transport;

    public class SqsTransport : TransportDefinition
    {
        public SqsTransport()
        {
            HasNativePubSubSupport = false;
			HasSupportForDistributedTransactions = false;
        }

        public override string ExampleConnectionStringForErrorMessage
        {
            get { return "Region=ap-southeast-2;S3BucketForLargeMessages=myBucketName;S3KeyPrefix=my/key/prefix;"; }
        }

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            throw new NotImplementedException();
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
