namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AmazonSQS;
    using Transport;

    class MessagePump : IPushMessages
    {
        public MessagePump(TransportConfiguration configuration, InputQueuePump inputQueuePump, DelayedMessagesPump delayedMessagesPump)
        {
            this.configuration = configuration;
            this.inputQueuePump = inputQueuePump;
            this.delayedMessagesPump = delayedMessagesPump;
        }

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            var inputQueueUrl = await inputQueuePump
                .Initialize(onMessage, onError, criticalError, settings).ConfigureAwait(false);

            if (configuration.IsDelayedDeliveryEnabled)
            {
                await delayedMessagesPump.Initialize(settings.InputQueue, inputQueueUrl).ConfigureAwait(false);
            }
        }

        public void Start(PushRuntimeSettings limitations)
        {
            cancellationTokenSource = new CancellationTokenSource();

            inputQueuePump.Start(limitations.MaxConcurrency, cancellationTokenSource.Token);

            if (configuration.IsDelayedDeliveryEnabled)
            {
                delayedMessagesPump.Start(cancellationTokenSource.Token);
            }
        }

        public async Task Stop()
        {
            cancellationTokenSource?.Cancel();

            await inputQueuePump.Stop().ConfigureAwait(false);

            if (configuration.IsDelayedDeliveryEnabled)
            {
                await delayedMessagesPump.Stop().ConfigureAwait(false);
            }
        }

        CancellationTokenSource cancellationTokenSource;
        TransportConfiguration configuration;

        readonly DelayedMessagesPump delayedMessagesPump;
        readonly InputQueuePump inputQueuePump;
    }
}