namespace NServiceBus.Transport.SQS.Configure;

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Transport;

class SqsTransportInfrastructure : TransportInfrastructure
{
    public SqsTransportInfrastructure(HostSettings hostSettings, ReceiveSettings[] receiverSettings,
        IAmazonSQS sqsClient,
        IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache,
        S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds,
        int? visibilityTimeoutInSeconds, TimeSpan maxAutoMessageVisibilityRenewalDuration, string topicNamePrefix,
        bool doNotWrapOutgoingMessages,
        bool shouldDisposeSqsClient, bool shouldDisposeSnsClient, bool disableDelayedDelivery,
        long reserveBytesInMessageSizeCalculation,
        Func<OutgoingMessage, string> messageGroupIdSelector)
    {
        this.sqsClient = sqsClient;
        this.snsClient = snsClient;
        this.queueCache = queueCache;
        this.shouldDisposeSqsClient = shouldDisposeSqsClient;
        this.shouldDisposeSnsClient = shouldDisposeSnsClient;
        s3Client = s3Settings?.S3Client;
        shouldDisposeS3Client = s3Settings is { ShouldDisposeS3Client: true };
        Receivers = receiverSettings
            .Select(receiverSetting => CreateMessagePump(receiverSetting, sqsClient, snsClient, queueCache, hostSettings.SetupInfrastructure, disableDelayedDelivery, topicCache, s3Settings, policySettings, queueDelayTimeSeconds, visibilityTimeoutInSeconds, maxAutoMessageVisibilityRenewalDuration, topicNamePrefix, hostSettings.CriticalErrorAction))
            .ToDictionary(x => x.Id, x => x);

        Dispatcher = new MessageDispatcher(hostSettings.CoreSettings, sqsClient, snsClient, queueCache, topicCache, s3Settings,
            queueDelayTimeSeconds, reserveBytesInMessageSizeCalculation, !doNotWrapOutgoingMessages, messageGroupIdSelector);
    }

    static IMessageReceiver CreateMessagePump(ReceiveSettings receiveSettings, IAmazonSQS sqsClient,
        IAmazonSimpleNotificationService snsClient, QueueCache queueCache, bool setupInfrastructure, bool disableDelayedDelivery,
        TopicCache topicCache, S3Settings s3Settings, PolicySettings policySettings, int queueDelayTimeSeconds,
        int? visibilityTimeoutInSeconds, TimeSpan maxAutoMessageVisibilityRenewalDuration, string topicNamePrefix,
        Action<string, Exception, CancellationToken> criticalErrorAction)
    {
        var receiveAddress = ToTransportAddressCore(receiveSettings.ReceiveAddress, queueCache);
        var subManager = new SubscriptionManager(sqsClient, snsClient, receiveAddress, queueCache, topicCache, policySettings, topicNamePrefix, setupInfrastructure);

        return new MessagePump(receiveSettings.Id, receiveAddress, receiveSettings.ErrorQueue, receiveSettings.PurgeOnStartup, sqsClient, queueCache, s3Settings, subManager, queueDelayTimeSeconds, visibilityTimeoutInSeconds, maxAutoMessageVisibilityRenewalDuration, criticalErrorAction, setupInfrastructure, disableDelayedDelivery);
    }

    public override async Task Shutdown(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.WhenAll(Receivers.Values.Select(pump => pump.StopReceive(cancellationToken)))
                .ConfigureAwait(false);
        }
        finally
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
        }
    }

    public override string ToTransportAddress(QueueAddress address) => ToTransportAddressCore(address, queueCache);

    static string ToTransportAddressCore(QueueAddress address, QueueCache queueCache)
    {
        var queueName = address.BaseAddress;
        var queue = new StringBuilder(queueName);
        if (address.Discriminator != null)
        {
            queue.Append($"-{address.Discriminator}");
        }

        if (address.Qualifier != null)
        {
            queue.Append($"-{address.Qualifier}");
        }

        return queueCache.GetPhysicalQueueName(queue.ToString());
    }

    readonly QueueCache queueCache;
    readonly IAmazonSQS sqsClient;
    readonly IAmazonSimpleNotificationService snsClient;
    readonly IAmazonS3 s3Client;
    readonly bool shouldDisposeSqsClient;
    readonly bool shouldDisposeSnsClient;
    readonly bool shouldDisposeS3Client;
}