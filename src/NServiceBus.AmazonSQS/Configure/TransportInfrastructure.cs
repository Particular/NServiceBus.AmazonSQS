namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SQS;
    using AmazonSQS;
    using DelayedDelivery;
    using Performance.TimeToBeReceived;
    using Routing;
    using Settings;
    using Transport;
    using Transports.SQS;

    class TransportInfrastructure : Transport.TransportInfrastructure
    {
        public TransportInfrastructure(ReadOnlySettings settings)
        {
            configuration = new ConnectionConfiguration(settings);

            sqsClient = configuration.SqsClientFactory();
            s3Client = configuration.S3ClientFactory();

            queueUrlCache = new QueueUrlCache(sqsClient);
        }


        public override IEnumerable<Type> DeliveryConstraints => new List<Type>
        {
            typeof(DiscardIfNotReceivedBefore),
            typeof(DoNotDeliverBefore),
            typeof(DelayDeliveryWith)
        };

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override OutboundRoutingPolicy OutboundRoutingPolicy
            => new OutboundRoutingPolicy(OutboundRoutingType.Unicast,
                OutboundRoutingType.Unicast,
                OutboundRoutingType.Unicast);

        MessagePump CreateMessagePump()
        {
            return new MessagePump(configuration, s3Client, sqsClient, queueUrlCache);
        }

        QueueCreator CreateQueueCreator()
        {
            return new QueueCreator(configuration, s3Client, sqsClient, queueUrlCache);
        }

        MessageDispatcher CreateMessageDispatcher()
        {
            return new MessageDispatcher(configuration, s3Client, sqsClient, queueUrlCache);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                CreateMessagePump,
                CreateQueueCreator,
                () => new RequiresWindows().Validate());
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                CreateMessageDispatcher,
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override Task Stop()
        {
            sqsClient.Dispose();
            s3Client.Dispose();
            return base.Stop();
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            throw new NotImplementedException("NServiceBus.AmazonSQS does not support native pub/sub.");
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance;
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var queueName = logicalAddress.EndpointInstance.Endpoint;
            var queue = new StringBuilder(queueName);
            if (logicalAddress.EndpointInstance.Discriminator != null)
            {
                queue.Append("-" + logicalAddress.EndpointInstance.Discriminator);
            }
            if (logicalAddress.Qualifier != null)
            {
                queue.Append("-" + logicalAddress.Qualifier);
            }
            return queue.ToString();
        }

        readonly IAmazonSQS sqsClient;
        readonly IAmazonS3 s3Client;
        readonly QueueUrlCache queueUrlCache;
        readonly ConnectionConfiguration configuration;
    }
}