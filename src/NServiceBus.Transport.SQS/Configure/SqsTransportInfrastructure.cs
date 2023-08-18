namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Settings;
    using Transport;

    class SqsTransportInfrastructure : TransportInfrastructure
    {
        public SqsTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receiverSettings, IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds, string topicNamePrefix, bool v1Compatibility, bool doNotWrapOutgoingMessages,
            bool shouldDisposeSqsClient, bool shouldDisposeSnsClient)
        {
            this.sqsClient = sqsClient;
            this.snsClient = snsClient;
            this.queueCache = queueCache;
            this.shouldDisposeSqsClient = shouldDisposeSqsClient;
            this.shouldDisposeSnsClient = shouldDisposeSnsClient;
            coreSettings = hostSettings.CoreSettings;
            s3Client = s3Settings?.S3Client;
            setupInfrastructure = hostSettings.SetupInfrastructure;
            shouldDisposeS3Client = s3Settings is { ShouldDisposeS3Client: true };
            Receivers = receiverSettings
                .Select(receiverSetting => CreateMessagePump(receiverSetting, sqsClient, snsClient, queueCache, topicCache, s3Settings, policySettings, queueDelayTimeSeconds, topicNamePrefix, hostSettings.CriticalErrorAction))
                .ToDictionary(x => x.Id, x => x);

            Dispatcher = new MessageDispatcher(hostSettings.CoreSettings, sqsClient, snsClient, queueCache, topicCache, s3Settings,
                queueDelayTimeSeconds, v1Compatibility, !doNotWrapOutgoingMessages);
        }

        IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient, QueueCache queueCache,
            TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds,
            string topicNamePrefix, Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            var receiveAddress = ToTransportAddress(receiveSettings.ReceiveAddress);
            var subManager = new SubscriptionManager(sqsClient, snsClient, receiveAddress, queueCache, topicCache, policySettings, topicNamePrefix, setupInfrastructure);

            return new MessagePump(receiveSettings.Id, receiveAddress, receiveSettings.ErrorQueue, receiveSettings.PurgeOnStartup, sqsClient, queueCache, s3Settings, subManager, queueDelayTimeSeconds, criticalErrorAction, coreSettings, setupInfrastructure);
        }

        public override Task Shutdown(CancellationToken cancellationToken = default)
        {
            if (shouldDisposeSqsClient)
            {
                sqsClient.Dispose();
            }

            if (shouldDisposeSnsClient)
            {
                snsClient.Dispose();
            }

            if (shouldDisposeS3Client)
            {
                s3Client?.Dispose();
            }

            return Task.CompletedTask;
        }

        public override string ToTransportAddress(QueueAddress address)
        {
            var queueName = address.BaseAddress;
            var queue = new StringBuilder(queueName);
            if (address.Discriminator != null)
            {
                queue.Append("-" + address.Discriminator);
            }

            if (address.Qualifier != null)
            {
                queue.Append("-" + address.Qualifier);
            }

            return queueCache.GetPhysicalQueueName(queue.ToString());
        }

        readonly QueueCache queueCache;
        readonly IAmazonSQS sqsClient;
        readonly IAmazonSimpleNotificationService snsClient;
        readonly IAmazonS3 s3Client;
        readonly IReadOnlySettings coreSettings;
        readonly bool setupInfrastructure;
        readonly bool shouldDisposeSqsClient;
        readonly bool shouldDisposeSnsClient;
        readonly bool shouldDisposeS3Client;
    }
}