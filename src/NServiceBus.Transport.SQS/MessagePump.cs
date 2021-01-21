namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SQS;

    class MessagePump : IMessageReceiver
    {
        readonly InputQueuePump inputQueuePump;
        readonly DelayedMessagesPump delayedMessagesPump;

        public MessagePump(ReceiveSettings settings, IAmazonSQS sqsClient, QueueCache queueCache, S3Settings s3Settings,
            SubscriptionManager subscriptionManager, int queueDelayTimeSeconds,
            Action<string, Exception> criticalErrorAction)
        {
            inputQueuePump = new InputQueuePump(settings, sqsClient, queueCache, s3Settings, subscriptionManager, criticalErrorAction);
            delayedMessagesPump = new DelayedMessagesPump(settings.ReceiveAddress, sqsClient, queueCache, queueDelayTimeSeconds);
        }

        public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError)
        {
            await inputQueuePump.Initialize(limitations, onMessage, onError).ConfigureAwait(false);
            await delayedMessagesPump.Initialize().ConfigureAwait(false);
        }

        public async Task StartReceive()
        {
            await inputQueuePump.StartReceive().ConfigureAwait(false);
            delayedMessagesPump.Start();
        }

        public async Task StopReceive()
        {
            await delayedMessagesPump.Stop().ConfigureAwait(false);
            await inputQueuePump.StopReceive().ConfigureAwait(false);
        }

        public ISubscriptionManager Subscriptions => inputQueuePump.Subscriptions;
        public string Id => inputQueuePump.Id;
    }
}