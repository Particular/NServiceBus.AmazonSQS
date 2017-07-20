namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using AmazonSQS;
    using Extensibility;
    using Logging;
    using Newtonsoft.Json;
    using Transport;

    class RepeatedFailuresOverTimeCircuitBreaker : IDisposable
    {
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering, Action<Exception> triggerAction)
        {
            this.name = name;
            this.triggerAction = triggerAction;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

            timer = new Timer(CircuitBreakerTriggered);
        }

        public void Dispose()
        {
            //Injected
        }

        public void Success()
        {
            var oldValue = Interlocked.Exchange(ref failureCount, 0);

            if (oldValue == 0)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
        }

        public Task Failure(Exception exception)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }

            return Task.Delay(TimeSpan.FromSeconds(1));
        }

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
                triggerAction(lastException);
            }
        }

        long failureCount;
        Exception lastException;

        string name;
        Timer timer;
        TimeSpan timeToWaitBeforeTriggering;
        Action<Exception> triggerAction;
        static TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
    }

    class BackoffStrategy
    {
        readonly TimeSpan peekInterval;
        readonly TimeSpan maximumWaitTimeWhenIdle;

        TimeSpan timeToDelayNextPeek;

        /// <summary>
        /// </summary>
        /// <param name="peekInterval">The amount of time, in milliseconds, to add to the time to wait before checking for a new message</param>
        /// <param name="maximumWaitTimeWhenIdle">The maximum amount of time that the queue will wait before checking for a new message</param>
        public BackoffStrategy(TimeSpan peekInterval, TimeSpan maximumWaitTimeWhenIdle)
        {
            this.peekInterval = peekInterval;
            this.maximumWaitTimeWhenIdle = maximumWaitTimeWhenIdle;
        }

        void OnSomethingProcessed()
        {
            timeToDelayNextPeek = TimeSpan.Zero;
        }

        Task OnNothingProcessed(CancellationToken token)
        {
            if (timeToDelayNextPeek + peekInterval < maximumWaitTimeWhenIdle)
            {
                timeToDelayNextPeek += peekInterval;
            }
            else
            {
                timeToDelayNextPeek = maximumWaitTimeWhenIdle;
            }

            return Task.Delay(timeToDelayNextPeek, token);
        }

        public Task OnBatch(int receivedBatchSize, CancellationToken token)
        {
            if (receivedBatchSize > 0)
            {
                OnSomethingProcessed();
                return TaskEx.CompletedTask;
            }

            return OnNothingProcessed(token);
        }
    }

    class MessageReceiver
    {
        IAmazonSQS sqsClient;
        QueueUrlCache queueUrlCache;
        readonly ConnectionConfiguration configuration;
        string inputQueue;
        string errorQueue;

        public MessageReceiver(ConnectionConfiguration configuration, IAmazonSQS sqsClient, IAmazonS3 s3Client, QueueUrlCache queueUrlCache, BackoffStrategy backoffStrategy)
        {
            this.s3Client = s3Client;
            this.backoffStrategy = backoffStrategy;
            this.configuration = configuration;
            this.queueUrlCache = queueUrlCache;
            this.sqsClient = sqsClient;
        }

        /// <summary>
        /// Sets whether or not the transport should purge the input
        /// queue when it is started.
        /// </summary>
        public bool PurgeOnStartup { get; set; }

        /// <summary>
        /// Controls how long messages should be invisible to other callers when receiving messages from the queue
        /// </summary>
        public TimeSpan MessageInvisibleTime { get; set; }

        /// <summary>
        /// Controls the number of messages that will be read in bulk from the queue
        /// </summary>
        public int BatchSize { get; set; }

        public async Task Init(string inputQueueAddress, string errorQueueAddress)
        {
            inputQueue = queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(inputQueueAddress, configuration));
            errorQueue = queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(errorQueueAddress, configuration));

            request = new ReceiveMessageRequest
            {
                MaxNumberOfMessages = BatchSize,
                QueueUrl = inputQueue,
                WaitTimeSeconds = 20, // make configurable
                AttributeNames = new List<String>
                {
                    "SentTimestamp"
                }
            };

            if (PurgeOnStartup)
            {
                // SQS only allows purging a queue once every 60 seconds or so.
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown.
                // This will happen if you are trying to start an endpoint twice or more
                // in that time.
                try
                {
                    await sqsClient.PurgeQueueAsync(inputQueue).ConfigureAwait(false);
                }
                catch (PurgeQueueInProgressException ex)
                {
                    Logger.Warn("Multiple queue purges within 60 seconds are not permitted by SQS.", ex);
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown from PurgeQueue.", ex);
                    throw;
                }
            }
        }

        public async Task Receive(List<MessageRetrieved> messages, CancellationToken token)
        {
            var receiveResult = await sqsClient.ReceiveMessageAsync(request, token).ConfigureAwait(false);

            foreach (var rawMessage in receiveResult.Messages)
            {
                messages.Add(new MessageRetrieved(sqsClient, s3Client, configuration, rawMessage, inputQueue, errorQueue));
            }

            await backoffStrategy.OnBatch(messages.Count, token).ConfigureAwait(false);
        }

        ReceiveMessageRequest request;
        static ILog Logger = LogManager.GetLogger<MessagePump>();
        BackoffStrategy backoffStrategy;
        IAmazonS3 s3Client;
    }

    static class TaskEx
    {
        // ReSharper disable once UnusedParameter.Global
        // Used to explicitly suppress the compiler warning about
        // using the returned value from async operations
        public static void Ignore(this Task task)
        {
        }

        //TODO: remove when we update to 4.6 and can use Task.CompletedTask
        public static readonly Task CompletedTask = Task.FromResult(0);
    }

    class MessagePumpV2 : IPushMessages
    {
        public MessagePumpV2(MessageReceiver messageReceiver, ConnectionConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache, int? degreeOfReceiveParallelism)
        {
            this.messageReceiver = messageReceiver;
            this.degreeOfReceiveParallelism = degreeOfReceiveParallelism;
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("SQS-MessagePump", TimeToWaitBeforeTriggering, ex => criticalError.Raise("Failed to receive message from SQS.", ex));
            messageReceiver.PurgeOnStartup = settings.PurgeOnStartup;

            receiveStrategy = ReceiveStrategy.BuildReceiveStrategy(onMessage, onError, settings.RequiredTransactionMode);

            return messageReceiver.Init(settings.InputQueue, settings.ErrorQueue);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            maximumConcurrency = limitations.MaxConcurrency;
            concurrencyLimiter = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
            cancellationTokenSource = new CancellationTokenSource();

            if (!degreeOfReceiveParallelism.HasValue)
            {
                degreeOfReceiveParallelism = EstimateDegreeOfReceiveParallelism(maximumConcurrency);
            }

            messagePumpTasks = new Task[degreeOfReceiveParallelism.Value];

            cancellationToken = cancellationTokenSource.Token;

            for (var i = 0; i < degreeOfReceiveParallelism; i++)
            {
                messagePumpTasks[i] = Task.Run(ProcessMessages, CancellationToken.None);
            }
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                using (var timeoutTokensource = new CancellationTokenSource(StoppingAllTasksTimeout))
                using (timeoutTokensource.Token.Register(() => tcs.TrySetCanceled())) // ok to have closure alloc here
                {
                    while (concurrencyLimiter.CurrentCount != maximumConcurrency)
                    {
                        await Task.Delay(50, timeoutTokensource.Token).ConfigureAwait(false);
                    }

                    await Task.WhenAny(Task.WhenAll(messagePumpTasks), tcs.Task).ConfigureAwait(false);
                    tcs.TrySetResult(true); // if we reach this WhenAll was successful
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
        }

        [DebuggerNonUserCode]
        async Task ProcessMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error("Polling Dequeue Strategy failed", ex);
                }
            }
        }

        async Task InnerProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var messages = new List<MessageRetrieved>();
                    await messageReceiver.Receive(messages, cancellationTokenSource.Token).ConfigureAwait(false);
                    circuitBreaker.Success();

                    foreach (var message in messages)
                    {
                        await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                        if (cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }

                        InnerReceive(message, concurrencyLimiter, receiveStrategy, cancellationToken).Ignore();
                    }
                }
                catch (OperationCanceledException)
                {
                    // For graceful shutdown purposes
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Receiving from the queue failed", ex);
                    await circuitBreaker.Failure(ex).ConfigureAwait(false);
                }
            }
        }

        static async Task InnerReceive(MessageRetrieved retrieved, SemaphoreSlim concurrencyLimiter, ReceiveStrategy receiveStrategy, CancellationToken token)
        {
            try
            {
                var message = await retrieved.Unwrap(token).ConfigureAwait(false);

                await receiveStrategy.Receive(retrieved, message).ConfigureAwait(false);
            }
            catch (LeaseTimeoutException ex)
            {
                Logger.Warn("Dispatching the message took longer than a visibility timeout. The message will reappear in the queue and will be obtained again.", ex);
            }
            catch (SerializationException ex)
            {
                Logger.Warn(ex.Message, ex);
            }
            catch (Exception ex)
            {
                Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline", ex);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        }

        static int EstimateDegreeOfReceiveParallelism(int maxConcurrency)
        {
            /*
             * Degree of parallelism = square root of MaxConcurrency (rounded)
             *  (concurrency 1 = 1, concurrency 10 = 3, concurrency 20 = 4, concurrency 50 = 7, concurrency 100 [default] = 10, concurrency 200 = 14, concurrency 1000 = 32)
             */
            return Math.Min(Convert.ToInt32(Math.Round(Math.Sqrt(Convert.ToDouble(maxConcurrency)))), 100);
        }

        TimeSpan timeToWaitBeforeTriggering;
        ConnectionConfiguration configuration;
        IAmazonS3 s3Client;
        IAmazonSQS sqsClient;
        ReceiveStrategy receiveStrategy;
        static TimeSpan TimeToWaitBeforeTriggering = TimeSpan.FromSeconds(30);
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        int maximumConcurrency;
        SemaphoreSlim concurrencyLimiter;
        CancellationTokenSource cancellationTokenSource;
        static ILog Logger = LogManager.GetLogger<MessagePump>();
        int? degreeOfReceiveParallelism;
        Task[] messagePumpTasks;
        CancellationToken cancellationToken;
        static TimeSpan StoppingAllTasksTimeout = TimeSpan.FromSeconds(30);
        MessageReceiver messageReceiver;
    }

    class LeaseTimeoutException : Exception
    {
        public LeaseTimeoutException(Message rawMessage, TimeSpan visibilityTimeoutExceededBy) : base($"The pop receipt of the cloud queue message '{rawMessage.MessageId}' is invalid as it exceeded the next visible time by '{visibilityTimeoutExceededBy}'.")
        {
        }
    }

    class MessageRetrieved
    {
        Message rawMessage;
        string inputQueue;
        string errorQueue;
        IAmazonSQS sqsClient;
        ConnectionConfiguration configuration;
        IAmazonS3 s3Client;

        public MessageRetrieved(IAmazonSQS sqsClient, IAmazonS3 s3Client, ConnectionConfiguration configuration, Message rawMessage, string inputQueue, string errorQueue)
        {
            this.s3Client = s3Client;
            this.configuration = configuration;
            this.sqsClient = sqsClient;
            this.rawMessage = rawMessage;
            this.inputQueue = inputQueue;
            this.errorQueue = errorQueue;
        }

        /// <summary>
        /// Unwraps the raw message body.
        /// </summary>
        /// <exception cref="SerializationException">Thrown when the raw message could not be unwrapped. The raw message is automatically moved to the error queue before this exception is thrown.</exception>
        /// <returns>The actual message wrapper.</returns>
        public async Task<TransportMessage> Unwrap(CancellationToken token)
        {
            try
            {
                var transportMessage = JsonConvert.DeserializeObject<TransportMessage>(rawMessage.Body);
                return await transportMessage.Materialize(s3Client, configuration, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // todo send to error queue
                // await errorQueue.AddMessageAsync(rawMessage).ConfigureAwait(false);
                await sqsClient.DeleteMessageAsync(inputQueue, rawMessage.ReceiptHandle, CancellationToken.None).ConfigureAwait(false);

                throw new SerializationException($"Failed to deserialize message envelope for message with id {rawMessage.MessageId}.", ex);
            }
        }
    }

    abstract class ReceiveStrategy
    {
        public abstract Task Receive(MessageRetrieved retrieved, TransportMessage message);

        public static ReceiveStrategy BuildReceiveStrategy(Func<MessageContext, Task> pipe, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe, TransportTransactionMode transactionMode)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (transactionMode)
            {
                case TransportTransactionMode.None:
                    return new AtMostOnceReceiveStrategy(pipe, errorPipe);
                case TransportTransactionMode.ReceiveOnly:
                    return new AtLeastOnceReceiveStrategy(pipe, errorPipe);
                default:
                    throw new NotSupportedException($"The TransportTransactionMode {transactionMode} is not supported");
            }
        }

        protected static ErrorContext CreateErrorContext(MessageRetrieved retrieved, MessageWrapper message, Exception ex, byte[] body)
        {
            var context = new ErrorContext(ex, message.Headers, message.Id, body, new TransportTransaction(), retrieved.DequeueCount);
            return context;
        }
    }

    class AtLeastOnceReceiveStrategy : ReceiveStrategy
    {
        public AtLeastOnceReceiveStrategy(Func<MessageContext, Task> pipe, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe)
        {
            this.pipeline = pipe;
            this.errorPipe = errorPipe;
        }

        public override async Task Receive(MessageRetrieved retrieved, TransportMessage message)
        {
            var body = message.ByteBody ?? new byte[0];
            try
            {
                using (var tokenSource = new CancellationTokenSource())
                {
                    var pushContext = new MessageContext(message.MessageId, message.Headers, body, new TransportTransaction(), tokenSource, new ContextBag());
                    await pipeline(pushContext).ConfigureAwait(false);

                    if (tokenSource.IsCancellationRequested)
                    {
                        // if the pipeline cancelled the execution, nack the message to go back to the queue
                        await retrieved.Nack().ConfigureAwait(false);
                    }
                    else
                    {
                        // the pipeline hasn't been cancelled, the message should be acked
                        await retrieved.Ack().ConfigureAwait(false);
                    }
                }
            }
            catch (LeaseTimeoutException)
            {
                // The lease has expired and cannot be used any longer to Ack or Nack the message.
                // see original issue: https://github.com/Azure/azure-storage-net/issues/285
                throw;
            }
            catch (Exception ex)
            {
                var context = CreateErrorContext(retrieved, message, ex, body);
                ErrorHandleResult immediateRetry;

                try
                {
                    immediateRetry = await errorPipe(context).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Warn("The error pipeline wasn't able to handle the exception.", e);
                    await retrieved.Nack().ConfigureAwait(false);
                    return;
                }

                if (immediateRetry == ErrorHandleResult.RetryRequired)
                {
                    // For an immediate retry, the error is logged and the message is returned to the queue to preserve the DequeueCount.
                    // There is no in memory retry as scale-out scenarios would be handled improperly.
                    Logger.Warn("Azure Storage Queue transport failed pushing a message through pipeline. The message will be requeued", ex);
                    await retrieved.Nack().ConfigureAwait(false);
                }
                else
                {
                    // Just acknowledge the message as it's handled by the core retry.
                    await retrieved.Ack().ConfigureAwait(false);
                }
            }
        }

        readonly Func<MessageContext, Task> pipeline;
        readonly Func<ErrorContext, Task<ErrorHandleResult>> errorPipe;

        static readonly ILog Logger = LogManager.GetLogger<AtLeastOnceReceiveStrategy>();
    }

    class AtMostOnceReceiveStrategy : ReceiveStrategy
    {
        public AtMostOnceReceiveStrategy(Func<MessageContext, Task> pipe, Func<ErrorContext, Task<ErrorHandleResult>> errorPipe)
        {
            throw new NotImplementedException();
        }

        public override Task Receive(MessageRetrieved retrieved, TransportMessage message)
        {
            throw new NotImplementedException();
        }
    }


    //class MessagePump : IPushMessages
    //{
    //    public MessagePump(ConnectionConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache)
    //    {
    //        this.configuration = configuration;
    //        this.s3Client = s3Client;
    //        this.sqsClient = sqsClient;
    //        this.queueUrlCache = queueUrlCache;
    //    }

    //    public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
    //    {
    //        queueUrl = queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(settings.InputQueue, configuration));

    //        if (settings.PurgeOnStartup)
    //        {
    //            // SQS only allows purging a queue once every 60 seconds or so.
    //            // If you try to purge a queue twice in relatively quick succession,
    //            // PurgeQueueInProgressException will be thrown.
    //            // This will happen if you are trying to start an endpoint twice or more
    //            // in that time.
    //            try
    //            {
    //                await sqsClient.PurgeQueueAsync(queueUrl).ConfigureAwait(false);
    //            }
    //            catch (PurgeQueueInProgressException ex)
    //            {
    //                Logger.Warn("Multiple queue purges within 60 seconds are not permitted by SQS.", ex);
    //            }
    //            catch (Exception ex)
    //            {
    //                Logger.Error("Exception thrown from PurgeQueue.", ex);
    //                throw;
    //            }
    //        }

    //        this.onMessage = onMessage;
    //        this.onError = onError;
    //    }

    //    public void Start(PushRuntimeSettings limitations)
    //    {
    //        cancellationTokenSource = new CancellationTokenSource();
    //        concurrencyLevel = limitations.MaxConcurrency;
    //        maxConcurrencySempahore = new SemaphoreSlim(concurrencyLevel);
    //        consumerTasks = new List<Task>();

    //        for (var i = 0; i < concurrencyLevel; i++)
    //        {
    //            consumerTasks.Add(ConsumeMessages());
    //        }
    //    }

    //    public async Task Stop()
    //    {
    //        cancellationTokenSource?.Cancel();

    //        try
    //        {
    //            await Task.WhenAll(consumerTasks.ToArray()).ConfigureAwait(false);
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            // Silently swallow OperationCanceledException
    //            // These are expected to be thrown when _cancellationTokenSource
    //            // is Cancel()ed above.
    //        }

    //        cancellationTokenSource?.Dispose();
    //        cancellationTokenSource = null;
    //    }

    //    async Task ConsumeMessages()
    //    {
    //        while (!cancellationTokenSource.IsCancellationRequested)
    //        {
    //            try
    //            {
    //                var receiveResult = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
    //                    {
    //                        MaxNumberOfMessages = 10,
    //                        QueueUrl = queueUrl,
    //                        WaitTimeSeconds = 20,
    //                        AttributeNames = new List<String>
    //                        {
    //                            "SentTimestamp"
    //                        }
    //                    },
    //                    cancellationTokenSource.Token).ConfigureAwait(false);

    //                var tasks = receiveResult.Messages.Select(async message =>
    //                {
    //                    IncomingMessage incomingMessage = null;
    //                    TransportMessage transportMessage = null;
    //                    var transportTransaction = new TransportTransaction();
    //                    var contextBag = new ContextBag();

    //                    var messageProcessedOk = false;
    //                    var messageExpired = false;
    //                    var isPoisonMessage = false;
    //                    var errorHandled = false;

    //                    try
    //                    {
    //                        transportMessage = JsonConvert.DeserializeObject<TransportMessage>(message.Body);

    //                        incomingMessage = await transportMessage.Materialize(
    //                            s3Client,
    //                            configuration,
    //                            cancellationTokenSource.Token).ConfigureAwait(false);
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        // Can't deserialize. This is a poison message
    //                        Logger.Warn($"Deleting poison message with SQS Message Id {message.MessageId} due to exception {ex}");
    //                        isPoisonMessage = true;
    //                    }

    //                    if (incomingMessage == null || transportMessage == null)
    //                    {
    //                        Logger.Warn($"Deleting poison message with SQS Message Id {message.MessageId}");
    //                        isPoisonMessage = true;
    //                    }

    //                    if (isPoisonMessage)
    //                    {
    //                        await DeleteMessage(sqsClient,
    //                            s3Client,
    //                            message,
    //                            transportMessage,
    //                            incomingMessage).ConfigureAwait(false);
    //                    }
    //                    else
    //                    {
    //                        // Check that the message hasn't expired
    //                        string rawTtbr;
    //                        if (incomingMessage.Headers.TryGetValue(TransportHeaders.TimeToBeReceived, out rawTtbr))
    //                        {
    //                            incomingMessage.Headers.Remove(TransportHeaders.TimeToBeReceived);

    //                            var timeToBeReceived = TimeSpan.Parse(rawTtbr);

    //                            if (timeToBeReceived != TimeSpan.MaxValue)
    //                            {
    //                                var sentDateTime = message.GetSentDateTime();
    //                                var utcNow = DateTime.UtcNow;
    //                                if (sentDateTime + timeToBeReceived <= utcNow)
    //                                {
    //                                    // Message has expired.
    //                                    Logger.Warn($"Discarding expired message with Id {incomingMessage.MessageId}");
    //                                    messageExpired = true;
    //                                }
    //                            }
    //                        }


    //                        if (!messageExpired)
    //                        {
    //                            var immediateProcessingAttempts = 0;

    //                            while (!errorHandled && !messageProcessedOk)
    //                            {
    //                                try
    //                                {
    //                                    await maxConcurrencySempahore
    //                                        .WaitAsync(cancellationTokenSource.Token)
    //                                        .ConfigureAwait(false);

    //                                    using (var messageContextCancellationTokenSource =
    //                                        new CancellationTokenSource())
    //                                    {
    //                                        var messageContext = new MessageContext(
    //                                            incomingMessage.MessageId,
    //                                            incomingMessage.Headers,
    //                                            incomingMessage.Body,
    //                                            transportTransaction,
    //                                            messageContextCancellationTokenSource,
    //                                            contextBag);

    //                                        await onMessage(messageContext)
    //                                            .ConfigureAwait(false);

    //                                        messageProcessedOk = !messageContextCancellationTokenSource.IsCancellationRequested;
    //                                    }
    //                                }
    //                                catch (Exception ex)
    //                                    when (!(ex is OperationCanceledException && cancellationTokenSource.IsCancellationRequested))
    //                                {
    //                                    immediateProcessingAttempts++;
    //                                    var errorHandlerResult = ErrorHandleResult.RetryRequired;

    //                                    try
    //                                    {
    //                                        errorHandlerResult = await onError(new ErrorContext(ex,
    //                                            incomingMessage.Headers,
    //                                            incomingMessage.MessageId,
    //                                            incomingMessage.Body,
    //                                            transportTransaction,
    //                                            immediateProcessingAttempts)).ConfigureAwait(false);
    //                                    }
    //                                    catch (Exception onErrorEx)
    //                                    {
    //                                        Logger.Error("Exception thrown from error handler", onErrorEx);
    //                                    }
    //                                    errorHandled = errorHandlerResult == ErrorHandleResult.Handled;
    //                                }
    //                                finally
    //                                {
    //                                    maxConcurrencySempahore.Release();
    //                                }
    //                            }
    //                        }

    //                        // Always delete the message from the queue.
    //                        // If processing failed, the onError handler will have moved the message
    //                        // to a retry queue.
    //                        await DeleteMessage(sqsClient, s3Client, message, transportMessage, incomingMessage).ConfigureAwait(false);
    //                    }
    //                });

    //                await Task.WhenAll(tasks).ConfigureAwait(false);
    //            }
    //            catch (Exception ex)
    //                when (!(ex is OperationCanceledException && cancellationTokenSource.IsCancellationRequested))
    //            {
    //                Logger.Error("Exception thrown when consuming messages", ex);
    //            }
    //        } // while
    //    }

    //    async Task DeleteMessage(IAmazonSQS sqs,
    //        IAmazonS3 s3,
    //        Message message,
    //        TransportMessage transportMessage,
    //        IncomingMessage incomingMessage)
    //    {
    //        await sqs.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationTokenSource.Token).ConfigureAwait(false);

    //        if (transportMessage != null)
    //        {
    //            if (!String.IsNullOrEmpty(transportMessage.S3BodyKey))
    //            {
    //                try
    //                {
    //                    await s3.DeleteObjectAsync(
    //                        new DeleteObjectRequest
    //                        {
    //                            BucketName = configuration.S3BucketForLargeMessages,
    //                            Key = configuration.S3KeyPrefix + incomingMessage.MessageId
    //                        },
    //                        cancellationTokenSource.Token).ConfigureAwait(false);
    //                }
    //                catch (Exception ex)
    //                {
    //                    // If deleting the message body from S3 fails, we don't
    //                    // want the exception to make its way through to the _endProcessMessage below,
    //                    // as the message has been successfully processed and deleted from the SQS queue
    //                    // and effectively doesn't exist anymore.
    //                    // It doesn't really matter, as S3 is configured to delete message body data
    //                    // automatically after a certain period of time.
    //                    Logger.Warn("Couldn't delete message body from S3. Message body data will be aged out by the S3 lifecycle policy when the TTL expires.", ex);
    //                }
    //            }
    //        }
    //        else
    //        {
    //            Logger.Warn("Couldn't delete message body from S3 because the TransportMessage was null. Message body data will be aged out by the S3 lifecycle policy when the TTL expires.");
    //        }
    //    }

    //    CancellationTokenSource cancellationTokenSource;
    //    List<Task> consumerTasks;
    //    Func<ErrorContext, Task<ErrorHandleResult>> onError;
    //    Func<MessageContext, Task> onMessage;
    //    SemaphoreSlim maxConcurrencySempahore;
    //    string queueUrl;
    //    int concurrencyLevel;
    //    ConnectionConfiguration configuration;
    //    IAmazonS3 s3Client;
    //    IAmazonSQS sqsClient;
    //    QueueUrlCache queueUrlCache;

    //    static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    //}
}