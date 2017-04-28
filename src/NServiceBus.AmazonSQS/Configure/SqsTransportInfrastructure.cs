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

            _sqsQueueUrlCache = new SqsQueueUrlCache()
            {
                SqsClient = _sqsClient
            };
        }

        SqsMessagePump CreateMessagePump()
        {
            var result = new SqsMessagePump()
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient
            };

            return result;
        }

        SqsQueueCreator CreateQueueCreator()
        {
            var result = new SqsQueueCreator()
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient
            };

            return result;
        }

        SqsMessageDispatcher CreateMessageDispatcher()
        {
            var result = new SqsMessageDispatcher()
            {
                ConnectionConfiguration = _connectionConfiguration,
                QueueCreator = CreateQueueCreator(),
                S3Client = _s3Client,
                SqsClient = _sqsClient
            };
            
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
            var sqsQueueName = SqsQueueNameHelper.GetSqsQueueName(logicalAddress.EndpointInstance.Endpoint,
                _connectionConfiguration);
            return _sqsQueueUrlCache.GetQueueUrl(sqsQueueName);
        }

        public override IEnumerable<Type> DeliveryConstraints => new List<Type>();

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;
        
        public override OutboundRoutingPolicy OutboundRoutingPolicy
            => new OutboundRoutingPolicy(OutboundRoutingType.Unicast,
                OutboundRoutingType.Unicast,
                OutboundRoutingType.Unicast);
    }
}
