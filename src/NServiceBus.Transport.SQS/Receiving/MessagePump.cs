namespace NServiceBus.Transport.SQS;

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;

class MessagePump : IMessageReceiver
{
    readonly bool disableDelayedDelivery;
    readonly InputQueuePump inputQueuePump;
    readonly DelayedMessagesPump delayedMessagesPump;

    public MessagePump(string receiverId,
        string receiveAddress,
        string errorQueueAddress,
        bool purgeOnStartup,
        IAmazonSQS sqsClient,
        QueueCache queueCache,
        S3Settings s3Settings,
        SubscriptionManager subscriptionManager,
        int queueDelayTimeSeconds,
        int? visibilityTimeoutInSeconds,
        TimeSpan maxAutoMessageVisibilityRenewalDuration,
        Action<string, Exception, CancellationToken> criticalErrorAction,
        bool setupInfrastructure,
        bool disableDelayedDelivery,
        bool doNotAutomaticallyPropagateMessageGroupId)
    {
        this.disableDelayedDelivery = disableDelayedDelivery;
        inputQueuePump = new InputQueuePump(receiverId, receiveAddress, errorQueueAddress, purgeOnStartup, sqsClient, queueCache, s3Settings, subscriptionManager, criticalErrorAction, visibilityTimeoutInSeconds, maxAutoMessageVisibilityRenewalDuration, setupInfrastructure);
        if (!disableDelayedDelivery)
        {
            delayedMessagesPump =
                new DelayedMessagesPump(receiveAddress, sqsClient, queueCache, queueDelayTimeSeconds, doNotAutomaticallyPropagateMessageGroupId);
        }
    }

    public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
    {
        await inputQueuePump.Initialize(limitations, onMessage, onError, cancellationToken).ConfigureAwait(false);
        if (!disableDelayedDelivery)
        {
            await delayedMessagesPump.Initialize(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StartReceive(CancellationToken cancellationToken = default)
    {
        await inputQueuePump.StartReceive(cancellationToken).ConfigureAwait(false);
        if (!disableDelayedDelivery)
        {
            delayedMessagesPump.Start(cancellationToken);
        }
    }

    public Task StopReceive(CancellationToken cancellationToken = default)
    {
        var stopDelayed = !disableDelayedDelivery
            ? delayedMessagesPump.Stop(cancellationToken)
            : Task.CompletedTask;
        var stopPump = inputQueuePump.StopReceive(cancellationToken);

        return Task.WhenAll(stopDelayed, stopPump);
    }

    public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default)
    {
        return inputQueuePump.ChangeConcurrency(limitations, cancellationToken);
    }

    public ISubscriptionManager Subscriptions => inputQueuePump.Subscriptions;
    public string Id => inputQueuePump.Id;
    public string ReceiveAddress => inputQueuePump.ReceiveAddress;
}