namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Transport;

    class SqsTransportInfrastructure : TransportInfrastructure
    {
        public SqsTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receiverSettings, IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds, string topicNamePrefix, bool v1Compatibility)
        {
            this.sqsClient = sqsClient;
            this.snsClient = snsClient;
            s3Client = s3Settings?.S3Client;
            Receivers = receiverSettings
                .Select(x => CreateMessagePump(x, sqsClient, snsClient, queueCache, topicCache, s3Settings, policySettings, queueDelayTimeSeconds, topicNamePrefix, hostSettings.CriticalErrorAction))
                .ToDictionary(x => x.Id, x => x);

            Dispatcher = new MessageDispatcher(sqsClient, snsClient, queueCache, topicCache, s3Settings,
                queueDelayTimeSeconds, v1Compatibility);
        }

        static IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, IAmazonSQS sqsClient,
            IAmazonSimpleNotificationService snsClient, QueueCache queueCache,
            TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds,
            string topicNamePrefix, Action<string, Exception> criticalErrorAction)
        {
            var subManager = new SubscriptionManager(sqsClient, snsClient, receiveSettings.ReceiveAddress, queueCache, topicCache, policySettings, topicNamePrefix);

            return new MessagePump(receiveSettings, sqsClient, queueCache, s3Settings, subManager, queueDelayTimeSeconds, criticalErrorAction);
        }

        public override Task Shutdown()
        {
            sqsClient.Dispose();
            snsClient.Dispose();
            s3Client?.Dispose();

            return Task.CompletedTask;
        }

        readonly IAmazonSQS sqsClient;
        readonly IAmazonSimpleNotificationService snsClient;
        readonly IAmazonS3 s3Client;
    }
}