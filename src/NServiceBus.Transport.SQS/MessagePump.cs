namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Settings;

    class MessagePump : IMessageReceiver
    {
        readonly InputQueuePump inputQueuePump;
        readonly DelayedMessagesPump delayedMessagesPump;

        public MessagePump(
            string receiverId,
            string receiveAddress,
            string errorQueueAddress,
            bool purgeOnStartup,
            IAmazonSQS sqsClient,
            QueueCache queueCache,
            S3Settings s3Settings,
            SubscriptionManager subscriptionManager,
            int queueDelayTimeSeconds,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            IReadOnlySettings coreSettings)
        {
            inputQueuePump = new InputQueuePump(receiverId, receiveAddress, errorQueueAddress, purgeOnStartup, sqsClient, queueCache, s3Settings, subscriptionManager, criticalErrorAction, coreSettings);
            delayedMessagesPump = new DelayedMessagesPump(receiveAddress, sqsClient, queueCache, queueDelayTimeSeconds);
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
            var stopDelayed = delayedMessagesPump.Stop(cancellationToken);
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
}