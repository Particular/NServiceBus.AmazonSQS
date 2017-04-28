namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using AmazonSQS;
    using Routing;
    using Transport;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.S3;
    using Settings;

    public class SqsTransportInfrastructure : TransportInfrastructure
    {
        readonly IAmazonSQS _sqsClient;
        readonly IAmazonS3 _s3Client;
        readonly SqsQueueUrlCache _sqsQueueUrlCache;
        readonly SqsConnectionConfiguration _connectionConfiguration;

        public SqsTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            _connectionConfiguration = SqsConnectionStringParser.Parse(connectionString);

            _sqsClient = AwsClientFactory.CreateSqsClient(_connectionConfiguration);
            _s3Client = AwsClientFactory.CreateS3Client(_connectionConfiguration);

            _sqsQueueUrlCache = new SqsQueueUrlCache();
            _sqsQueueUrlCache.ConnectionConfiguration = _connectionConfiguration;
            _sqsQueueUrlCache.SqsClient = _sqsClient;
        }
        
        SqsMessagePump CreateMessagePump()
        {
            var result = new SqsMessagePump();
            result.ConnectionConfiguration = _connectionConfiguration;
            result.S3Client = _s3Client;
            result.SqsClient = _sqsClient;
            return result;
        }

        SqsQueueCreator CreateQueueCreator()
        {
            var result = new SqsQueueCreator();
            result.ConnectionConfiguration = _connectionConfiguration;
            result.S3Client = _s3Client;
            result.SqsClient = _sqsClient;
            return result;
        }

        SqsMessageDispatcher CreateMessageDispatcher()
        {
            var result = new SqsMessageDispatcher();
            result.ConnectionConfiguration = _connectionConfiguration;
            result.QueueCreator = CreateQueueCreator();
            result.QueueUrlCache = _sqsQueueUrlCache;
            result.S3Client = _s3Client;
            result.SqsClient = _sqsClient;
            return result;
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
            return new TransportSubscriptionInfrastructure(
                () => null);
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance;
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            // NSB6 TODO: Convert to a Queue Url instead, use SqsQueueUrlCache
            return SqsQueueNameHelper.GetSqsQueueName(logicalAddress.EndpointInstance.Endpoint,
                _connectionConfiguration);
        }
        
        public override IEnumerable<Type> DeliveryConstraints
        {
            get
            {
                return new List<Type>();
            }
        }

        public override TransportTransactionMode TransactionMode
        {
            get
            {
                return TransportTransactionMode.ReceiveOnly;
            }
        }

        public override OutboundRoutingPolicy OutboundRoutingPolicy
        {
            get
            {
                return new OutboundRoutingPolicy(OutboundRoutingType.Unicast,
                    OutboundRoutingType.Unicast,
                    OutboundRoutingType.Unicast);
            }
        }
    }
}
