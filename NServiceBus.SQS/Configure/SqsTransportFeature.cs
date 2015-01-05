namespace NServiceBus.Features
{
	using NServiceBus.Settings;
	using NServiceBus.SQS;
	using NServiceBus.Transports;
	using NServiceBus.Transports.SQS;

    public class SqsTransportFeature : ConfigureTransport<SqsTransport>
    {
        public override void Initialize()
        {
			var connectionString = SettingsHolder.Get<string>("NServiceBus.Transport.ConnectionString");
            var connectionConfiguration = SqsConnectionStringParser.Parse(connectionString);

			NServiceBus.Configure.Component<SqsConnectionConfiguration>(_ => connectionConfiguration, DependencyLifecycle.SingleInstance);
				
			NServiceBus.Configure.Component<AwsClientFactory>(DependencyLifecycle.SingleInstance);

			NServiceBus.Configure.Component<SqsDequeueStrategy>(DependencyLifecycle.InstancePerCall)
				.ConfigureProperty(p => p.PurgeOnStartup, ConfigurePurging.PurgeRequested);

			NServiceBus.Configure.Component<SqsQueueSender>(DependencyLifecycle.InstancePerCall);

			NServiceBus.Configure.Component<SqsQueueCreator>(DependencyLifecycle.InstancePerCall);
        }

        protected override string ExampleConnectionStringForErrorMessage
        {
			get { return "Region=ap-southeast-2;S3BucketForLargeMessages=myBucketName;S3KeyPrefix=my/key/prefix;"; }
        }

		protected override void InternalConfigure(Configure config)
		{
			Enable<SqsTransportFeature>();
			Enable<MessageDrivenSubscriptions>();
		}
	}
}
