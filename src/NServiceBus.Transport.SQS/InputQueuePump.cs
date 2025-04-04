namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using BitFaster.Caching.Lru;
    using Configure;
    using Extensibility;
    using Extensions;
    using Logging;
    using Settings;
    using Message = Amazon.SQS.Model.Message;

    class InputQueuePump : IMessageReceiver
    {
        public InputQueuePump(
            string receiverId,
            string receiveAddress,
            string errorQueueAddress,
            bool purgeOnStartup,
            IAmazonSQS sqsClient,
            QueueCache queueCache,
            S3Settings s3Settings,
            SubscriptionManager subscriptionManager,
            Action<string, Exception, CancellationToken> criticalErrorAction,
            IReadOnlySettings coreSettings,
            bool setupInfrastructure = true)
        {
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
            this.s3Settings = s3Settings;
            this.criticalErrorAction = criticalErrorAction;
            this.errorQueueAddress = errorQueueAddress;
            this.purgeOnStartup = purgeOnStartup;
            Id = receiverId;
            ReceiveAddress = receiveAddress;
            Subscriptions = subscriptionManager;

            this.coreSettings = coreSettings;
            this.setupInfrastructure = setupInfrastructure;
        }

        public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = default)
        {
            try
            {
                inputQueueUrl = await queueCache.GetQueueUrl(ReceiveAddress, cancellationToken).ConfigureAwait(false);
            }
            catch (QueueDoesNotExistException ex)
            {
                var msg = setupInfrastructure
                    ? $"Queue `{ReceiveAddress}` doesn't exist. Ensure this process has the required permissions to create queues on Amazon SQS."
                    : $"Queue `{ReceiveAddress}` doesn't exist. Call endpointConfiguration.EnableInstallers() to create the queues at startup, or create them manually.";
                throw new QueueDoesNotExistException(
                        msg,
                        ex,
                        ex.ErrorType,
                        ex.ErrorCode,
                        ex.RequestId,
                        ex.StatusCode);
            }

            maxConcurrency = limitations.MaxConcurrency;

            if (purgeOnStartup)
            {
                // SQS only allows purging a queue once every 60 seconds or so.
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown.
                // This will happen if you are trying to start an endpoint twice or more
                // in that time.
                try
                {
                    await sqsClient.PurgeQueueAsync(inputQueueUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (PurgeQueueInProgressException ex)
                {
                    Logger.Warn("Multiple queue purges within 60 seconds are not permitted by SQS.", ex);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                {
                    Logger.Error("Exception thrown from PurgeQueue.", ex);
                    throw;
                }
            }

            this.onMessage = onMessage;
            this.onError = onError;
        }

        public Task StartReceive(CancellationToken cancellationToken = default)
        {
            if (messagePumpCancellationTokenSource != null)
            {
                return Task.CompletedTask; //Receiver already started.
            }

            messagePumpCancellationTokenSource = new CancellationTokenSource();
            messageProcessingCancellationTokenSource = new CancellationTokenSource();

            int numberOfPumps;
            if (maxConcurrency <= 10)
            {
                numberOfPumps = 1;
                numberOfMessagesToFetch = maxConcurrency;
            }
            else
            {
                numberOfMessagesToFetch = 10;
                numberOfPumps = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(maxConcurrency) / numberOfMessagesToFetch));
            }

            receiveMessagesRequest = new ReceiveMessageRequest
            {
                MaxNumberOfMessages = numberOfMessagesToFetch,
                QueueUrl = inputQueueUrl,
                WaitTimeSeconds = 20,
                MessageSystemAttributeNames = ["ApproximateReceiveCount", "SentTimestamp"],
                MessageAttributeNames = ["All"]
            };

            if (coreSettings != null && coreSettings.TryGet<int>(SettingsKeys.MessageVisibilityTimeout, out var visibilityTimeout))
            {
                receiveMessagesRequest.VisibilityTimeout = visibilityTimeout;
            }

            maxConcurrencySemaphore = new SemaphoreSlim(maxConcurrency);
            pumpTasks = new List<Task>(numberOfPumps);

            for (var i = 0; i < numberOfPumps; i++)
            {
                // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
                pumpTasks.Add(Task.Run(() => PumpMessagesAndSwallowExceptions(messagePumpCancellationTokenSource.Token), CancellationToken.None));
            }

            return Task.CompletedTask;
        }

        public async Task StopReceive(CancellationToken cancellationToken = default)
        {
            if (messagePumpCancellationTokenSource == null)
            {
                // already stopped or not yet started
                return;
            }

            await messagePumpCancellationTokenSource.CancelAsync().ConfigureAwait(false);

            await using (cancellationToken.Register(() => messageProcessingCancellationTokenSource.Cancel()))
            {
                if (pumpTasks != null)
                {
                    await Task.WhenAll(pumpTasks).ConfigureAwait(false);
                    pumpTasks = null;
                }

                while (maxConcurrencySemaphore.CurrentCount != maxConcurrency)
                {
                    // Want to let the message pump drain naturally, which will happen quickly after
                    // messageProcessingCancellationTokenSource begins killing processing pipelines
                    await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
                }
            }

            messagePumpCancellationTokenSource.Dispose();
            messageProcessingCancellationTokenSource.Dispose();
            maxConcurrencySemaphore.Dispose();
            messagePumpCancellationTokenSource = null;
        }

        public async Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default)
        {
            await StopReceive(cancellationToken).ConfigureAwait(false);
            maxConcurrency = limitations.MaxConcurrency;
            await StartReceive(cancellationToken).ConfigureAwait(false);
        }

        public ISubscriptionManager Subscriptions { get; }
        public string Id { get; }
        public string ReceiveAddress { get; }

        async Task PumpMessagesAndSwallowExceptions(CancellationToken messagePumpCancellationToken)
        {
            while (!messagePumpCancellationToken.IsCancellationRequested)
            {
#pragma warning disable PS0021 // Highlight when a try block passes multiple cancellation tokens - justification:
                // The message processing cancellation token is used for processing,
                // since we only want that to be cancelled when the public token passed to Stop() is cancelled.
                // The message pump token is being used elsewhere, because we want those operations to be cancelled as soon as Stop() is called.
                // The catch clause is correctly filtered on the message pump cancellation token.
                try
                {
#pragma warning restore PS0021 // Highlight when a try block passes multiple cancellation tokens
                    var receivedMessages = await sqsClient.ReceiveMessageAsync(receiveMessagesRequest, messagePumpCancellationToken).ConfigureAwait(false);

                    foreach (var receivedMessage in receivedMessages.Messages)
                    {
                        await maxConcurrencySemaphore.WaitAsync(messagePumpCancellationToken).ConfigureAwait(false);

                        // no Task.Run() here to avoid a closure
                        _ = ProcessMessageSwallowExceptionsAndReleaseMaxConcurrencySemaphore(receivedMessage, messageProcessingCancellationTokenSource.Token);
                    }
                }
                catch (Exception ex) when (ex.IsCausedBy(messagePumpCancellationToken))
                {
                    // private token, pump is being stopped, log exception in case stack trace is ever needed for debugging
                    Logger.Debug("Operation canceled while stopping input queue pump.");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming messages", ex);
                }
            }
        }

        async Task ProcessMessageSwallowExceptionsAndReleaseMaxConcurrencySemaphore(Message receivedMessage, CancellationToken messageProcessingCancellationToken)
        {
            try
            {
                try
                {
                    await ProcessMessage(receivedMessage, messageProcessingCancellationToken).ConfigureAwait(false);
                }
#pragma warning disable PS0019 // Do not catch Exception without considering OperationCanceledException - handling is the same for OCE
                catch (Exception ex)
#pragma warning restore PS0019 // Do not catch Exception without considering OperationCanceledException - handling is the same for OCE
                {
                    Logger.Debug("Returning message to queue...", ex);

                    await ReturnMessageToQueue(receivedMessage).ConfigureAwait(false);

                    throw;
                }
            }
            catch (Exception ex) when (ex.IsCausedBy(messageProcessingCancellationToken))
            {
                Logger.Debug("Message processing canceled.", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Message processing failed.", ex);
            }
            finally
            {
                maxConcurrencySemaphore.Release();
            }
        }

        // the method should really be private, but it's internal for testing
#pragma warning disable PS0017 // Single, non-private CancellationToken parameters should be named cancellationToken
#pragma warning disable PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
        internal async Task ProcessMessage(Message receivedMessage, CancellationToken messageProcessingCancellationToken)
#pragma warning restore PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
#pragma warning restore PS0017 // Single, non-private CancellationToken parameters should be named cancellationToken
        {
            var arrayPool = ArrayPool<byte>.Shared;
            ReadOnlyMemory<byte> messageBody = null;
            byte[] messageBodyBuffer = null;
            TransportMessage transportMessage = null;
            Exception exception = null;
            var isPoisonMessage = false;
            var nativeMessageId = receivedMessage.MessageId;

            if (messagesToBeDeleted.TryGet(nativeMessageId, out _))
            {
                await DeleteMessage(receivedMessage, null).ConfigureAwait(false);
                return;
            }

            try
            {
                var messageId = ExtractMessageId(receivedMessage);

                try
                {
                    transportMessage = ExtractTransportMessage(receivedMessage, messageId);
                    messageId = transportMessage.Headers[Headers.MessageId];
                    (messageBody, messageBodyBuffer) = await transportMessage.RetrieveBody(messageId, s3Settings, arrayPool, messageProcessingCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
                {
                    // Can't deserialize. This is a poison message
                    exception = ex;
                    isPoisonMessage = true;
                }

                if (isPoisonMessage)
                {
                    var logMessage = $"Treating message with {messageId} as a poison message. Moving to error queue.";
                    Logger.Warn(logMessage, exception);

                    await MovePoisonMessageToErrorQueue(receivedMessage, messageProcessingCancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (IsMessageExpired(receivedMessage, transportMessage.Headers, messageId, sqsClient.Config.ClockOffset))
                {
                    await DeleteMessage(receivedMessage, transportMessage.S3BodyKey).ConfigureAwait(false);
                }
                else
                {
                    // here we also want to use the native message id because the core demands it like that
                    var messageProcessed = await InnerProcessMessage(transportMessage.Headers, nativeMessageId, messageBody, receivedMessage, messageProcessingCancellationToken).ConfigureAwait(false);

                    if (messageProcessed)
                    {
                        await DeleteMessage(receivedMessage, transportMessage.S3BodyKey).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (messageBodyBuffer != null)
                {
                    arrayPool.Return(messageBodyBuffer, clearArray: true);
                }
            }
        }


        public static string ExtractMessageId(Message receivedMessage) =>
            receivedMessage.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute)
                ? messageIdAttribute.StringValue
                : receivedMessage.MessageId;

        public static TransportMessage ExtractTransportMessage(Message receivedMessage, string messageIdOverride)
        {
            TransportMessage transportMessage;
            if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.Headers, out var headersAttribute))
            {
                transportMessage = new TransportMessage
                {
                    Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersAttribute.StringValue) ?? [],
                    Body = receivedMessage.Body
                };
                if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.S3BodyKey, out var s3BodyKey))
                {
                    transportMessage.Headers[TransportHeaders.S3BodyKey] = s3BodyKey.StringValue;
                    transportMessage.S3BodyKey = s3BodyKey.StringValue;
                }
            }
            else
            {
                // When the MessageTypeFullName attribute is available, we're assuming native integration
                if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.MessageTypeFullName, out var enclosedMessageType))
                {
                    var headers = new Dictionary<string, string>
                    {
                                { Headers.MessageId, messageIdOverride },
                                { Headers.EnclosedMessageTypes, enclosedMessageType.StringValue },
                                {
                                    TransportHeaders.MessageTypeFullName, enclosedMessageType.StringValue
                                } // we're copying over the value of the native message attribute into the headers, converting this into a nsb message
                            };

                    if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.S3BodyKey, out var s3BodyKey))
                    {
                        headers.Add(TransportHeaders.S3BodyKey, s3BodyKey.StringValue);
                    }

                    transportMessage = new TransportMessage
                    {
                        Headers = headers,
                        S3BodyKey = s3BodyKey?.StringValue,
                        Body = receivedMessage.Body
                    };
                }
                else
                {
                    try
                    {
                        transportMessage = JsonSerializer.Deserialize<TransportMessage>(receivedMessage.Body, transportMessageSerializerOptions);

                        if (CouldBeNativeMessage(transportMessage))
                        {
                            Logger.Debug($"Message with native id {receivedMessage.MessageId} does not contain the required information and will not be treated as an NServiceBus TransportMessage. " +
                                   $"Instead it'll be treated as pure native message.");

                            transportMessage = new TransportMessage
                            {
                                Body = receivedMessage.Body,
                                Headers = new Dictionary<string, string>
                                {
                                    // HINT: Message Id is a required field for InnerProcessMessage
                                    [Headers.MessageId] = receivedMessage.MessageId,
                                }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        //HINT: Deserialization is best-effort. If it fails, we trat the message as a native message
                        Logger.Debug($"Failed to deserialize message with native id {receivedMessage.MessageId}. " +
                                     $"It will not be treated as an NServiceBus TransportMessage. Instead it'll be treated as pure native message.", ex);

                        transportMessage = new TransportMessage
                        {
                            Body = receivedMessage.Body,
                            Headers = new Dictionary<string, string>
                            {
                                // HINT: Message Id is a required field for InnerProcessMessage
                                [Headers.MessageId] = receivedMessage.MessageId,
                            }
                        };
                    }
                }
            }
            // HINT: Message Id is the only required header
            transportMessage.Headers.TryAdd(Headers.MessageId, messageIdOverride);
            return transportMessage;
        }

        static bool CouldBeNativeMessage(TransportMessage msg)
        {
            if (msg.Headers == null)
            {
                return true;
            }

            if (msg.Headers.ContainsKey(Headers.ControlMessageHeader) &&
                msg.Headers[Headers.ControlMessageHeader] == true.ToString())
            {
                return false;
            }

            if (!msg.Headers.ContainsKey(Headers.MessageId) &&
                !msg.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            {
                return true;
            }

            return false;
        }

        async Task<bool> InnerProcessMessage(Dictionary<string, string> headers, string nativeMessageId, ReadOnlyMemory<byte> body, Message nativeMessage, CancellationToken messageProcessingCancellationToken)
        {
            // set the native message on the context for advanced usage scenario's
            var context = new ContextBag();
            context.Set(nativeMessage);
            // We add it to the transport transaction to make it available in dispatching scenario's so we copy over message attributes when moving messages to the error/audit queue
            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(nativeMessage);
            transportTransaction.Set("IncomingMessageId", headers[Headers.MessageId]);

            try
            {
                var messageContext = new MessageContext(
                    nativeMessageId,
                    new Dictionary<string, string>(headers),
                    body,
                    transportTransaction,
                    ReceiveAddress,
                    context);

                await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                var deliveryAttempts = GetDeliveryAttempts(nativeMessageId, nativeMessage);

                try
                {
                    var errorHandlerResult = await onError(new ErrorContext(ex,
                        new Dictionary<string, string>(headers),
                        nativeMessageId,
                        body,
                        transportTransaction,
                        deliveryAttempts,
                        ReceiveAddress,
                        context), messageProcessingCancellationToken).ConfigureAwait(false);

                    if (errorHandlerResult == ErrorHandleResult.RetryRequired)
                    {
                        await ReturnMessageToQueue(nativeMessage).ConfigureAwait(false);

                        return false;
                    }
                }
                catch (Exception onErrorEx) when (!onErrorEx.IsCausedBy(messageProcessingCancellationToken))
                {
                    criticalErrorAction(
                        $"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`",
                        onErrorEx,
                        messageProcessingCancellationToken);

                    await ReturnMessageToQueue(nativeMessage).ConfigureAwait(false);

                    return false;
                }
            }

            return true;
        }


#pragma warning disable PS0018 //Cancellation token intentionally not passed because cancellation shouldn't stop messages from being returned to the queue
        async Task ReturnMessageToQueue(Message message)
#pragma warning restore PS0018
        {
            try
            {
                await sqsClient.ChangeMessageVisibilityAsync(
                    new ChangeMessageVisibilityRequest()
                    {
                        QueueUrl = inputQueueUrl,
                        ReceiptHandle = message.ReceiptHandle,
                        VisibilityTimeout = 0
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Warn($"Failed to return message with native ID '{message.MessageId}' to the queue because the visibility timeout has expired. The message has already been returned to the queue.", ex);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to return message with native ID '{message.MessageId}' to the queue. The message will return to the queue after the visibility timeout expires.", ex);
            }
        }

        int GetDeliveryAttempts(string nativeMessageId, Message message)
        {
            var attempts = deliveryAttempts.GetOrAdd(nativeMessageId, k => 0);

            attempts++;

            if (message.Attributes.TryGetValue("ApproximateReceiveCount", out var countStr) &&
                int.TryParse(countStr, out var count))
            {
                attempts = Math.Max(attempts, count);
            }

            deliveryAttempts.AddOrUpdate(nativeMessageId, attempts);
            return attempts;
        }

        static bool IsMessageExpired(Message receivedMessage, Dictionary<string, string> headers, string messageId, TimeSpan clockOffset)
        {
            if (!headers.Remove(TransportHeaders.TimeToBeReceived, out var rawTtbr))
            {
                return false;
            }

            var timeToBeReceived = TimeSpan.Parse(rawTtbr);
            if (timeToBeReceived == TimeSpan.MaxValue)
            {
                return false;
            }

            var sentAt = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("SentTimestamp", clockOffset);
            var expiresAt = sentAt + timeToBeReceived;
            var now = DateTimeOffset.UtcNow;
            if (expiresAt > now)
            {
                return false;
            }

            // Message has expired.
            Logger.Info($"Discarding expired message with Id {messageId}, expired {now - expiresAt} ago at {expiresAt} utc.");
            return true;
        }

#pragma warning disable PS0018 // Do not add CancellationToken parameter - delete should not be cancellable
        async Task DeleteMessage(Message message, string s3BodyKey)
#pragma warning restore PS0018 //  Do not add CancellationToken parameter - delete should not be cancellable
        {
            try
            {
                await sqsClient
                    .DeleteMessageAsync(inputQueueUrl, message.ReceiptHandle, CancellationToken.None)
                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(s3BodyKey))
                {
                    Logger.Info($"Message body data with key '{s3BodyKey}' will be aged out by the S3 lifecycle policy when the TTL expires.");
                }
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Error($"Failed to delete message with native ID '{message.MessageId}' because the handler execution time exceeded the visibility timeout. Increase the length of the timeout on the queue. The message was returned to the queue.", ex);

                messagesToBeDeleted.AddOrUpdate(message.MessageId, true);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to delete message with native ID '{message.MessageId}'. The message will be returned to the queue when the visibility timeout expires.", ex);

                messagesToBeDeleted.AddOrUpdate(message.MessageId, true);
            }
        }

        async Task MovePoisonMessageToErrorQueue(Message message, CancellationToken cancellationToken)
        {
            string errorQueueUrl = null;
            try
            {
                errorQueueUrl = await queueCache.GetQueueUrl(errorQueueAddress, cancellationToken)
                    .ConfigureAwait(false);
                // Ok to use LINQ here since this is not really a hot path
                var messageAttributeValues = message.MessageAttributes
                    .ToDictionary(pair => pair.Key, messageAttribute => messageAttribute.Value);

                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = errorQueueUrl,
                    MessageBody = message.Body,
                    MessageAttributes = messageAttributeValues
                }, cancellationToken).ConfigureAwait(false);
                // The Attributes on message are read-only attributes provided by SQS
                // and can't be re-sent. Unfortunately all the SQS metadata
                // such as SentTimestamp is reset with this send.
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Error($"Error moving poison message to error queue at url {errorQueueUrl}. Moving back to input queue.", ex);
                try
                {
                    await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                    {
                        QueueUrl = inputQueueUrl,
                        ReceiptHandle = message.ReceiptHandle,
                        VisibilityTimeout = 0
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception changeMessageVisibilityEx) when (!changeMessageVisibilityEx.IsCausedBy(cancellationToken))
                {
                    Logger.Warn($"Error returning poison message with native ID '{message.MessageId}' back to input queue {inputQueueUrl}. Poison message will become available at the input queue again after the visibility timeout expires.", changeMessageVisibilityEx);
                }

                return;
            }

            try
            {
                await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = inputQueueUrl,
                    ReceiptHandle = message.ReceiptHandle
                }, CancellationToken.None) // We don't want the delete to be cancellable to avoid unnecessary duplicates of poison messages
                    .ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Error($"Error removing poison message with native ID '{message.MessageId}' from input queue {inputQueueUrl} because the visibility timeout expired. Poison message will become available at the input queue again and attempted to be removed on a best-effort basis. This may still cause duplicate poison messages in the error queue for this endpoint", ex);

                messagesToBeDeleted.AddOrUpdate(message.MessageId, true);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error removing poison message with native ID '{message.MessageId}' from input queue {inputQueueUrl}. Poison message will become available at the input queue again and attempted to be removed on a best-effort basis. This may still cause duplicate poison messages in the error queue for this endpoint", ex);

                messagesToBeDeleted.AddOrUpdate(message.MessageId, true);
            }

            // If there is a message body in S3, simply leave it there to be aged out by the S3 lifecycle policy when the TTL expires
        }

        List<Task> pumpTasks;
        OnError onError;
        OnMessage onMessage;
        SemaphoreSlim maxConcurrencySemaphore;
        string inputQueueUrl;
        int maxConcurrency;

        readonly FastConcurrentLru<string, int> deliveryAttempts = new(1_000);
        readonly FastConcurrentLru<string, bool> messagesToBeDeleted = new(1_000);
        readonly string errorQueueAddress;
        readonly bool purgeOnStartup;
        readonly IAmazonSQS sqsClient;
        readonly QueueCache queueCache;
        readonly S3Settings s3Settings;
        readonly Action<string, Exception, CancellationToken> criticalErrorAction;
        readonly IReadOnlySettings coreSettings;
        readonly bool setupInfrastructure;

        static readonly JsonSerializerOptions transportMessageSerializerOptions = new()
        {
            TypeInfoResolver = TransportMessageSerializerContext.Default
        };

        int numberOfMessagesToFetch;
        ReceiveMessageRequest receiveMessagesRequest;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;

        static readonly ILog Logger = LogManager.GetLogger<MessagePump>();
    }
}