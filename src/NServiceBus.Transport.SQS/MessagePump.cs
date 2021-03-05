namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;

    class MessagePump : IMessageReceiver
    {
        readonly InputQueuePump inputQueuePump;
        readonly DelayedMessagesPump delayedMessagesPump;

        public MessagePump(ReceiveSettings settings, IAmazonSQS sqsClient, QueueCache queueCache, S3Settings s3Settings,
            SubscriptionManager subscriptionManager, int queueDelayTimeSeconds,
            Action<string, Exception, CancellationToken> criticalErrorAction)
        {
            inputQueuePump = new InputQueuePump(settings, sqsClient, queueCache, s3Settings, subscriptionManager, criticalErrorAction);
            delayedMessagesPump = new DelayedMessagesPump(settings.ReceiveAddress, sqsClient, queueCache, queueDelayTimeSeconds);
        }

        public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            await inputQueuePump.Initialize(limitations, onMessage, onError, cancellationToken).ConfigureAwait(false);
            await delayedMessagesPump.Initialize(cancellationToken).ConfigureAwait(false);
        }

        public async Task StartReceive(CancellationToken cancellationToken = default)
        {
            await inputQueuePump.StartReceive(cancellationToken).ConfigureAwait(false);
            delayedMessagesPump.Start(cancellationToken);
        }

        public Task StopReceive(CancellationToken cancellationToken = default)
        {
            var stopDelayed = delayedMessagesPump.Stop();
            var stopPump = inputQueuePump.StopReceive(cancellationToken);

            return Task.WhenAll(stopDelayed, stopPump);
        }

        public ISubscriptionManager Subscriptions => inputQueuePump.Subscriptions;
        public string Id => inputQueuePump.Id;
    }
}