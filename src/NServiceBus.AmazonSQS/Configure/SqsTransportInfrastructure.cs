namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using AmazonSQS;
    using Routing;
    using Transport;

    public class SqsTransportInfrastructure : TransportInfrastructure
    {
        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            var connectionConfiguration = SqsConnectionStringParser.Parse(connectionString);

			context.Container.ConfigureComponent(_ => connectionConfiguration, DependencyLifecycle.SingleInstance);

            context.Container.ConfigureComponent(_ => AwsClientFactory.CreateSqsClient(connectionConfiguration), DependencyLifecycle.SingleInstance);

            context.Container.ConfigureComponent(_ => AwsClientFactory.CreateS3Client(connectionConfiguration), DependencyLifecycle.SingleInstance);

			context.Container.ConfigureComponent<SqsQueueUrlCache>(DependencyLifecycle.SingleInstance);

			context.Container.ConfigureComponent<SqsDequeueStrategy>(DependencyLifecycle.InstancePerCall);
				
			context.Container.ConfigureComponent<SqsQueueSender>(DependencyLifecycle.InstancePerCall);

			context.Container.ConfigureComponent<SqsQueueCreator>(DependencyLifecycle.InstancePerCall);

            if ( context.Settings.UseSqsDeferral() )
                context.Container.ConfigureComponent<SqsDeferrer>(DependencyLifecycle.InstancePerCall);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            throw new NotImplementedException();
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            throw new NotImplementedException();
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException();
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            throw new NotImplementedException();
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            throw new NotImplementedException();
        }

        protected override string ExampleConnectionStringForErrorMessage
        {
			get { return "Region=ap-southeast-2;S3BucketForLargeMessages=myBucketName;S3KeyPrefix=my/key/prefix;"; }
        }

        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override TransportTransactionMode TransactionMode
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override OutboundRoutingPolicy OutboundRoutingPolicy
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
