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
        public TransportInfrastructure(SettingsHolder settings)
        {
            _connectionConfiguration = new ConnectionConfiguration(settings);

            _sqsClient = AwsClientFactory.CreateSqsClient(_connectionConfiguration);
            _s3Client = AwsClientFactory.CreateS3Client(_connectionConfiguration);

            queueUrlCache = new QueueUrlCache
            {
                SqsClient = _sqsClient
            };
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
            return new MessagePump
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient,
                QueueUrlCache = queueUrlCache
            };
        }

        QueueCreator CreateQueueCreator()
        {
            return new QueueCreator
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient,
                QueueUrlCache = queueUrlCache
            };
        }

        MessageDispatcher CreateMessageDispatcher()
        {
            return new MessageDispatcher
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient,
                QueueUrlCache = queueUrlCache
            };
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                CreateMessagePump,
                CreateQueueCreator,
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
                CreateMessageDispatcher,
                () => Task.FromResult(StartupCheckResult.Success));
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

        readonly IAmazonSQS _sqsClient;
        readonly IAmazonS3 _s3Client;
        readonly QueueUrlCache queueUrlCache;
        readonly ConnectionConfiguration _connectionConfiguration;
    }
}