namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Settings;
    using Transport;

    class SqsTransportInfrastructure : TransportInfrastructure
    {
        public SqsTransportInfrastructure(SqsTransport transportDefinition, HostSettings hostSettings, ReceiveSettings[] receiverSettings, IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds, string topicNamePrefix, bool v1Compatibility, bool doNotBase64EncodeOutgoingMessages)
        {
            this.transportDefinition = transportDefinition;
            this.sqsClient = sqsClient;
            this.snsClient = snsClient;
            coreSettings = hostSettings.CoreSettings;
            s3Client = s3Settings?.S3Client;
            Receivers = receiverSettings
                .Select(receiverSetting => CreateMessagePump(receiverSetting, sqsClient, snsClient, queueCache, topicCache, s3Settings, policySettings, queueDelayTimeSeconds, topicNamePrefix, hostSettings.CriticalErrorAction))
                .ToDictionary(x => x.Id, x => x);

            Dispatcher = new MessageDispatcher(hostSettings.CoreSettings, sqsClient, snsClient, queueCache, topicCache, s3Settings,
                queueDelayTimeSeconds, v1Compatibility, !doNotBase64EncodeOutgoingMessages);
        }

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient, QueueCache queueCache,
            TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds,
            string topicNamePrefix, Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            var receiveAddress = ToTransportAddress(receiveSettings.ReceiveAddress);
            var subManager = new SubscriptionManager(sqsClient, snsClient, receiveAddress, queueCache, topicCache, policySettings, topicNamePrefix);

            return new MessagePump(receiveSettings.Id, receiveAddress, receiveSettings.ErrorQueue, receiveSettings.PurgeOnStartup, sqsClient, queueCache, s3Settings, subManager, queueDelayTimeSeconds, criticalErrorAction, coreSettings);
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            sqsClient.Dispose();
            snsClient.Dispose();
            s3Client?.Dispose();

            return Task.CompletedTask;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public override string ToTransportAddress(QueueAddress address) => transportDefinition.ToTransportAddress(address);
#pragma warning restore CS0618 // Type or member is obsolete

        readonly SqsTransport transportDefinition;
        readonly IAmazonSQS sqsClient;
        readonly IAmazonSimpleNotificationService snsClient;
        readonly IAmazonS3 s3Client;
        readonly IReadOnlySettings coreSettings;
    }
}