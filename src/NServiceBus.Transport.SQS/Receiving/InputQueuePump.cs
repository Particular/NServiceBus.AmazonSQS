namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using BitFaster.Caching.Lru;
    using Envelopes;
    using Extensions;
    using Logging;
    using Message = Amazon.SQS.Model.Message;

    class InputQueuePump(
        string receiverId,
        string receiveAddress,
        string errorQueueAddress,
        bool purgeOnStartup,
        IAmazonSQS sqsClient,
        QueueCache queueCache,
        S3Settings s3Settings,
        SubscriptionManager subscriptionManager,
        Action<string, Exception, CancellationToken> criticalErrorAction,
        int? configuredVisibilityTimeoutInSeconds,
        TimeSpan maxAutoMessageVisibilityRenewalDuration,
        bool setupInfrastructure = true)
        : IMessageReceiver
    {
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

            // An explicit visibility timeout takes precedence to control the receive request so there is no need to list the queue attributes in that case.
            if (configuredVisibilityTimeoutInSeconds.HasValue)
            {
                visibilityTimeoutInSeconds = configuredVisibilityTimeoutInSeconds.Value;
            }
            else
            {
                var queueAttributes = await sqsClient.GetQueueAttributesAsync(inputQueueUrl, [QueueAttributeName.VisibilityTimeout], cancellationToken)
                    .ConfigureAwait(false);
                visibilityTimeoutInSeconds = queueAttributes.VisibilityTimeout;
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

            // Only when the visibility timeout was explicitly configured on the transport it is necessary
            // to override it on the request.
            if (configuredVisibilityTimeoutInSeconds.HasValue)
            {
                receiveMessagesRequest.VisibilityTimeout = configuredVisibilityTimeoutInSeconds.Value;
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

        public ISubscriptionManager Subscriptions { get; } = subscriptionManager;
        public string Id { get; } = receiverId;
        public string ReceiveAddress { get; } = receiveAddress;

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

                    var visibilityExpiresOn = DateTimeOffset.UtcNow.AddSeconds(visibilityTimeoutInSeconds);
                    foreach (var receivedMessage in receivedMessages.Messages)
                    {
                        await maxConcurrencySemaphore.WaitAsync(messagePumpCancellationToken).ConfigureAwait(false);

                        _ = ProcessMessageWithVisibilityRenewal(receivedMessage, visibilityExpiresOn, cancellationToken: messageProcessingCancellationTokenSource.Token);
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

        internal async Task ProcessMessageWithVisibilityRenewal(Message receivedMessage, DateTimeOffset visibilityExpiresOn, TimeProvider timeProvider = null, CancellationToken cancellationToken = default)
        {
            timeProvider ??= TimeProvider.System;
            using var renewalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (maxAutoMessageVisibilityRenewalDuration == TimeSpan.Zero)
            {
                await renewalCancellationTokenSource.CancelAsync()
                    .ConfigureAwait(false);
            }
            else
            {
                renewalCancellationTokenSource.CancelAfter(maxAutoMessageVisibilityRenewalDuration);
            }

            using var messageVisibilityLostCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var renewalTask = Renewal.RenewMessageVisibility(receivedMessage, visibilityExpiresOn, visibilityTimeoutInSeconds,
                sqsClient, inputQueueUrl, messageVisibilityLostCancellationTokenSource, timeProvider,
                cancellationToken: renewalCancellationTokenSource.Token);

            ExceptionDispatchInfo processMessageFailed = null;

            try
            {
                // deliberately not passing renewalCancellationTokenSource into the processing method. There are scenarios where it is possible that you can still
                // successfully process the message even when the message visibility has expired. But when the messageVisibilityLostCancellationTokenSource gets cancelled
                // we know that we have lost the visibility and we should not process the message anymore. This gives the users a chance to stop processing.
                await ProcessMessage(receivedMessage, messageVisibilityLostCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (messageVisibilityLostCancellationTokenSource.Token.IsCancellationRequested)
            {
                processMessageFailed = ExceptionDispatchInfo.Capture(ex);
                Logger.Debug("Message processing canceled.", ex);
            }
            catch (Exception ex)
            {
                processMessageFailed = ExceptionDispatchInfo.Capture(ex);
                Logger.Error("Message processing failed.", ex);
            }
            finally
            {
                await renewalCancellationTokenSource.CancelAsync()
                    .ConfigureAwait(false);
                maxConcurrencySemaphore.Release();
            }

            var renewalResult = await renewalTask.ConfigureAwait(false);

            if (processMessageFailed is not null && renewalResult != Renewal.Result.Failed)
            {
                await ReturnMessageToQueue(receivedMessage).ConfigureAwait(false);
            }
        }

        // the method should really be private, but it's internal for testing
#pragma warning disable PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
        internal async Task ProcessMessage(Message receivedMessage, CancellationToken cancellationToken)
#pragma warning restore PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
        {
            var arrayPool = ArrayPool<byte>.Shared;
            byte[] messageBodyBuffer = null;
            TranslatedMessage translatedMessage = null;
            Exception exception = null;
            var isPoisonMessage = false;
            var nativeMessageId = receivedMessage.MessageId;
            MessageContext messageContext = null;

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
                    (messageContext, messageId, messageBodyBuffer) = await messageTranslation.CreateMessageContext(receivedMessage, messageId, ReceiveAddress, s3Settings, arrayPool, cancellationToken).ConfigureAwait(false);
                    //translatedMessage = messageTranslation.TranslateIncoming(receivedMessage, nativeMessageId);

                    // messageId = translationResult.messageId;
                    // messageBodyBuffer = translationResult.messageBodyBuffer;
                    // messageId = translatedMessage.Headers[Headers.MessageId];
                    // (messageBody, messageBodyBuffer) = await translatedMessage.RetrieveBody(messageId, s3Settings, arrayPool, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
                {
                    // Can't deserialize. This is a poison message
                    exception = ex;
                    isPoisonMessage = true;
                }

                if (isPoisonMessage)
                {
                    var logMessage = $"Treating message with {messageId} as a poison message. Moving to error queue.";
                    Logger.Warn(logMessage, exception);

                    await MovePoisonMessageToErrorQueue(receivedMessage, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (IsMessageExpired(receivedMessage, translatedMessage.Headers, messageId, sqsClient.Config.ClockOffset))
                {
                    await DeleteMessage(receivedMessage, translatedMessage.S3BodyKey).ConfigureAwait(false);
                }
                else
                {
                    // here we also want to use the native message id because the core demands it like that
                    var messageProcessed = await InnerProcessMessage(messageContext, cancellationToken).ConfigureAwait(false);
                    //var messageProcessed = await InnerProcessMessage(translatedMessage.Headers, nativeMessageId, messageBody, receivedMessage, cancellationToken).ConfigureAwait(false);

                    if (messageProcessed)
                    {
                        await DeleteMessage(receivedMessage, translatedMessage.S3BodyKey).ConfigureAwait(false);
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

        async Task<bool> InnerProcessMessage(MessageContext messageContext, CancellationToken messageProcessingCancellationToken)
        {
            try
            {
                await onMessage(messageContext, messageProcessingCancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(messageProcessingCancellationToken))
            {
                var nativeMessageId = messageContext.NativeMessageId;
                var nativeMessage = messageContext.Extensions.Get<Message>();
                var deliveryAttempts = GetDeliveryAttempts(nativeMessageId, nativeMessage);

                try
                {
                    var errorHandlerResult = await onError(new ErrorContext(ex,
                        messageContext.Headers,
                        nativeMessageId,
                        messageContext.Body,
                        messageContext.TransportTransaction,
                        deliveryAttempts,
                        ReceiveAddress,
                        messageContext.Extensions), messageProcessingCancellationToken).ConfigureAwait(false);

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

        // async Task<bool> InnerProcessMessage(Dictionary<string, string> headers, string nativeMessageId, ReadOnlyMemory<byte> body, Message nativeMessage, CancellationToken messageProcessingCancellationToken)
        // {
        //     // set the native message on the context for advanced usage scenario's
        //     var context = new ContextBag();
        //     context.Set(nativeMessage);
        //
        //     context.Set("EnvelopeFormat", "TBD");
        //     // We add it to the transport transaction to make it available in dispatching scenario's so we copy over message attributes when moving messages to the error/audit queue
        //     var transportTransaction = new TransportTransaction();
        //     transportTransaction.Set(nativeMessage);
        //     transportTransaction.Set("IncomingMessageId", headers[Headers.MessageId]);
        //
        //     return await InnerProcessMessage(new MessageContext(
        //         nativeMessageId,
        //         new Dictionary<string, string>(headers),
        //         body,
        //         transportTransaction,
        //         ReceiveAddress,
        //         context), messageProcessingCancellationToken).ConfigureAwait(false);
        // }


#pragma warning disable PS0018 //Cancellation token intentionally not passed because cancellation shouldn't stop messages from being returned to the queue
        async Task ReturnMessageToQueue(Message message)
#pragma warning restore PS0018
        {
            try
            {
                await sqsClient.ChangeMessageVisibilityAsync(
                    new ChangeMessageVisibilityRequest { QueueUrl = inputQueueUrl, ReceiptHandle = message.ReceiptHandle, VisibilityTimeout = 0 },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Warn($"Failed to return message with native ID '{message.MessageId}' to the queue because the receipt handle is not valid.", ex);
            }
            catch (AmazonSQSException ex) when (ex.IsCausedByMessageVisibilityExpiry())
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
            Logger.InfoFormat("Discarding expired message with Id {0}, expired {1} ago at {2} utc.", messageId, now - expiresAt, expiresAt);
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
                    Logger.InfoFormat("Message body data with key '{0}' will be aged out by the S3 lifecycle policy when the TTL expires.", s3BodyKey);
                }
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Error($"Failed to delete message with native ID '{message.MessageId}' because the receipt handle was invalid.", ex);

                messagesToBeDeleted.AddOrUpdate(message.MessageId, true);
            }
            catch (AmazonSQSException ex) when (ex.IsCausedByMessageVisibilityExpiry())
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

                await sqsClient.SendMessageAsync(new SendMessageRequest { QueueUrl = errorQueueUrl, MessageBody = message.Body, MessageAttributes = messageAttributeValues }, cancellationToken).ConfigureAwait(false);
                // The Attributes on message are read-only attributes provided by SQS
                // and can't be re-sent. Unfortunately all the SQS metadata
                // such as SentTimestamp is reset with this send.
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Error($"Error moving poison message to error queue at url {errorQueueUrl}. Moving back to input queue.", ex);
                try
                {
                    await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest { QueueUrl = inputQueueUrl, ReceiptHandle = message.ReceiptHandle, VisibilityTimeout = 0 }, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception changeMessageVisibilityEx) when (!changeMessageVisibilityEx.IsCausedBy(cancellationToken))
                {
                    Logger.Warn($"Error returning poison message with native ID '{message.MessageId}' back to input queue {inputQueueUrl}. Poison message will become available at the input queue again after the visibility timeout expires.", changeMessageVisibilityEx);
                }

                return;
            }

            try
            {
                await sqsClient.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = inputQueueUrl, ReceiptHandle = message.ReceiptHandle }, CancellationToken.None) // We don't want the delete to be cancellable to avoid unnecessary duplicates of poison messages
                    .ConfigureAwait(false);
            }
            catch (AmazonSQSException ex) when (ex.IsCausedByMessageVisibilityExpiry())
            {
                Logger.Error($"Error removing poison message with native ID '{message.MessageId}' from input queue {inputQueueUrl} because the visibility timeout expired. Poison message will become available at the input queue again and attempted to be removed on a best-effort basis. This may still cause duplicate poison messages in the error queue for this endpoint", ex);

                messagesToBeDeleted.AddOrUpdate(message.MessageId, true);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Error($"Error removing poison message with native ID '{message.MessageId}' from input queue {inputQueueUrl} because the receipt handle is not valid. Poison message will become available at the input queue again and attempted to be removed on a best-effort basis. This may still cause duplicate poison messages in the error queue for this endpoint", ex);

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

        int numberOfMessagesToFetch;
        ReceiveMessageRequest receiveMessagesRequest;
        CancellationTokenSource messagePumpCancellationTokenSource;
        CancellationTokenSource messageProcessingCancellationTokenSource;
        int visibilityTimeoutInSeconds;

        static readonly ILog Logger = LogManager.GetLogger<MessagePump>();

        static readonly MessageTranslation messageTranslation = MessageTranslation.Initialize();
    }
}