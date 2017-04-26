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
        
        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(
                () => new SqsDequeueStrategy(),
                () => new SqsQueueCreator(),
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(
               () => new SqsMessageDispatcher(),
               () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(
                () => null);
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            throw new NotImplementedException();
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            return SqsQueueNameHelper.GetSqsQueueName(logicalAddress.Qualifier);
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
