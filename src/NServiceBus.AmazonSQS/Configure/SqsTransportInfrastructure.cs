﻿namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using AmazonSQS;
    using Routing;
    using Transport;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.S3;
    using Performance.TimeToBeReceived;
    using System.Text;
    using Settings;
    using Transports.SQS;

    internal class SqsTransportInfrastructure : TransportInfrastructure
    {
        readonly IAmazonSQS _sqsClient;
        readonly IAmazonS3 _s3Client;
        readonly SqsQueueUrlCache _sqsQueueUrlCache;
        readonly SqsConnectionConfiguration _connectionConfiguration;

        public SqsTransportInfrastructure(SettingsHolder settings)
        {
            _connectionConfiguration = new SqsConnectionConfiguration(settings);

            _sqsClient = AwsClientFactory.CreateSqsClient(_connectionConfiguration);
            _s3Client = AwsClientFactory.CreateS3Client(_connectionConfiguration);

            _sqsQueueUrlCache = new SqsQueueUrlCache
            {
                SqsClient = _sqsClient
            };
        }

        SqsMessagePump CreateMessagePump()
        {
            return new SqsMessagePump
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient,
                SqsQueueUrlCache = _sqsQueueUrlCache
            };
        }

        SqsQueueCreator CreateQueueCreator()
        {
            return new SqsQueueCreator
            {
                ConnectionConfiguration = _connectionConfiguration,
                S3Client = _s3Client,
                SqsClient = _sqsClient,
                QueueUrlCache = _sqsQueueUrlCache,
            };
        }

        SqsMessageDispatcher CreateMessageDispatcher()
        {
            return new SqsMessageDispatcher
            {
                ConnectionConfiguration = _connectionConfiguration,
                QueueCreator = CreateQueueCreator(),
                S3Client = _s3Client,
                SqsClient = _sqsClient,
                SqsQueueUrlCache = _sqsQueueUrlCache
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
            string queueName = logicalAddress.EndpointInstance.Endpoint;
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


        public override IEnumerable<Type> DeliveryConstraints => new List<Type>
        {
            typeof(DiscardIfNotReceivedBefore)
        };

        public override TransportTransactionMode TransactionMode => TransportTransactionMode.ReceiveOnly;

        public override OutboundRoutingPolicy OutboundRoutingPolicy
            => new OutboundRoutingPolicy(OutboundRoutingType.Unicast,
                OutboundRoutingType.Unicast,
                OutboundRoutingType.Unicast);
    }
}
