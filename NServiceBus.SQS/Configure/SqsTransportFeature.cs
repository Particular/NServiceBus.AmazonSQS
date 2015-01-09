namespace NServiceBus.Features
{
	using NServiceBus.Settings;
	using NServiceBus.SQS;
	using NServiceBus.Transports;
	using NServiceBus.Transports.SQS;

    public class SqsTransportFeature : ConfigureTransport
    {
        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            var connectionConfiguration = SqsConnectionStringParser.Parse(connectionString);

			context.Container.ConfigureComponent<SqsConnectionConfiguration>(_ => connectionConfiguration, DependencyLifecycle.SingleInstance);

			context.Container.ConfigureComponent<AwsClientFactory>(DependencyLifecycle.SingleInstance);

			context.Container.ConfigureComponent<SqsQueueUrlCache>(DependencyLifecycle.SingleInstance);

			context.Container.ConfigureComponent<SqsDequeueStrategy>(DependencyLifecycle.InstancePerCall);
				
			context.Container.ConfigureComponent<SqsQueueSender>(DependencyLifecycle.InstancePerCall);

			context.Container.ConfigureComponent<SqsQueueCreator>(DependencyLifecycle.InstancePerCall);
        }

        protected override string ExampleConnectionStringForErrorMessage
        {
			get { return "Region=ap-southeast-2;S3BucketForLargeMessages=myBucketName;S3KeyPrefix=my/key/prefix;"; }
        }
	}
}
