namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensibility;
    using Extensions;
    using Logging;
    using SimpleJson;

    class InputQueuePump
    {
        public InputQueuePump(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueCache queueCache)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
        }

        public async Task<string> Initialize(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.criticalError = criticalError;

            inputQueueUrl = await queueCache.GetQueueUrl(settings.InputQueue)
                .ConfigureAwait(false);
            errorQueueUrl = await queueCache.GetQueueUrl(settings.ErrorQueue)
                .ConfigureAwait(false);

            if (settings.PurgeOnStartup)
            {
                // SQS only allows purging a queue once every 60 seconds or so.
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown.
                // This will happen if you are trying to start an endpoint twice or more
                // in that time.
                try
                {
                    await sqsClient.PurgeQueueAsync(inputQueueUrl, CancellationToken.None).ConfigureAwait(false);
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

            this.onMessage = onMessage;
            this.onError = onError;
            return inputQueueUrl;
        }

        public void Start(int maximumProcessingConcurrency, CancellationToken token)
        {
            maxConcurrency = maximumProcessingConcurrency;

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
                AttributeNames = new List<string> { "SentTimestamp" },
                MessageAttributeNames = new List<string> { "All" }
            };
            //Set visibilitytimeout only when explicitly set by user configuration, else take the value in the queue
            //users can define a custom visibility timeout only when using message driven pub/sub compatibility mode
            if (configuration.MessageVisibilityTimeout.HasValue)
            {
                receiveMessagesRequest.VisibilityTimeout = configuration.MessageVisibilityTimeout.Value;
            }

            maxConcurrencySemaphore = new SemaphoreSlim(maxConcurrency);
            pumpTasks = new List<Task>(numberOfPumps);

            for (var i = 0; i < numberOfPumps; i++)
            {
                pumpTasks.Add(Task.Run(() => ConsumeMessages(token), CancellationToken.None));
            }
        }

        public async Task Stop()
        {
            await Task.WhenAll(pumpTasks).ConfigureAwait(false);
            pumpTasks?.Clear();

            while (maxConcurrencySemaphore.CurrentCount != maxConcurrency)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            maxConcurrencySemaphore?.Dispose();
        }

        async Task ConsumeMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var receivedMessages = await sqsClient.ReceiveMessageAsync(receiveMessagesRequest, token).ConfigureAwait(false);

                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var receivedMessage in receivedMessages.Messages)
                    {
                        try
                        {
                            await maxConcurrencySemaphore.WaitAsync(token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // shutting, semaphore doesn't need to be released because it was never acquired
                            return;
                        }

                        try
                        {
                            _ = ProcessMessage(receivedMessage, maxConcurrencySemaphore, token);
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug("Returning message to queue...", ex);

                            await ReturnMessageToQueue(receivedMessage).ConfigureAwait(false);

                            throw;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // ignore for graceful shutdown
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming messages", ex);
                }
            } // while
        }

        internal async Task ProcessMessage(Message receivedMessage, SemaphoreSlim processingSemaphoreSlim, CancellationToken token)
        {
            try
            {
                byte[] messageBody = null;
                TransportMessage transportMessage = null;
                Exception exception = null;
                var nativeMessageId = receivedMessage.MessageId;
                string messageId = null;
                var isPoisonMessage = false;

                if (messagesToBeDeletedStorage.TryGetFailureInfoForMessage(nativeMessageId, out _))
                {
                    await DeleteMessage(receivedMessage, null).ConfigureAwait(false);
                    return;
                }

                try
                {
                    if (receivedMessage.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute))
                    {
                        messageId = messageIdAttribute.StringValue;
                    }
                    else
                    {
                        messageId = nativeMessageId;
                    }

                    if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.Headers, out var headersAttribute))
                    {
                        transportMessage = new TransportMessage { Headers = SimpleJson.DeserializeObject<Dictionary<string, string>>(headersAttribute.StringValue) ?? new Dictionary<string, string>(), Body = receivedMessage.Body };
                        transportMessage.Headers[Headers.MessageId] = messageId;
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
                                { Headers.MessageId, messageId }, { Headers.EnclosedMessageTypes, enclosedMessageType.StringValue }, { TransportHeaders.MessageTypeFullName, enclosedMessageType.StringValue } // we're copying over the value of the native message attribute into the headers, converting this into a nsb message
                            };

                            if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.S3BodyKey, out var s3BodyKey))
                            {
                                headers.Add(TransportHeaders.S3BodyKey, s3BodyKey.StringValue);
                            }

                            transportMessage = new TransportMessage { Headers = headers, S3BodyKey = s3BodyKey?.StringValue, Body = receivedMessage.Body };
                        }
                        else
                        {
                            transportMessage = SimpleJson.DeserializeObject<TransportMessage>(receivedMessage.Body);
                        }
                    }

                    messageBody = await transportMessage.RetrieveBody(configuration, s3Client, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // shutting down
                    return;
                }
                catch (Exception ex)
                {
                    // Can't deserialize. This is a poison message
                    exception = ex;
                    isPoisonMessage = true;
                }

                if (isPoisonMessage || transportMessage == null || messageBody == null)
                {
                    var logMessage = $"Treating message with {messageId} as a poison message. Moving to error queue.";

                    if (exception != null)
                    {
                        Logger.Warn(logMessage, exception);
                    }
                    else
                    {
                        Logger.Warn(logMessage);
                    }

                    await MovePoisonMessageToErrorQueue(receivedMessage).ConfigureAwait(false);
                    return;
                }

                if (IsMessageExpired(receivedMessage, transportMessage.Headers, messageId, sqsClient.Config.ClockOffset))
                {
                    await DeleteMessage(receivedMessage, transportMessage.S3BodyKey).ConfigureAwait(false);
                }
                else
                {
                    // here we also want to use the native message id because the core demands it like that
                    var messageProcessed = await InnerProcessMessage(transportMessage.Headers, nativeMessageId, messageBody, receivedMessage, token).ConfigureAwait(false);

                    if (messageProcessed)
                    {
                        await DeleteMessage(receivedMessage, transportMessage.S3BodyKey).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                processingSemaphoreSlim.Release();
            }
        }

        async Task<bool> InnerProcessMessage(Dictionary<string, string> headers, string nativeMessageId, byte[] body, Message nativeMessage, CancellationToken token)
        {
            // set the native message on the context for advanced usage scenario's
            var context = new ContextBag();
            context.Set(nativeMessage);
            // We add it to the transport transaction to make it available in dispatching scenario's so we copy over message attributes when moving messages to the error/audit queue
            var transportTransaction = new TransportTransaction();
            transportTransaction.Set(nativeMessage);
            transportTransaction.Set("IncomingMessageId", headers[Headers.MessageId]);

            using (var messageContextCancellationTokenSource = new CancellationTokenSource())
            {
                try
                {

                    var messageContext = new MessageContext(
                        nativeMessageId,
                        new Dictionary<string, string>(headers),
                        body,
                        transportTransaction,
                        messageContextCancellationTokenSource,
                        context);

                    await onMessage(messageContext).ConfigureAwait(false);

                }
                catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested))
                {
                    var deliveryAttempts = GetDeliveryAttempts(nativeMessageId);

                    try
                    {
                        var errorHandlerResult = await onError(new ErrorContext(ex,
                            new Dictionary<string, string>(headers),
                            nativeMessageId,
                            body.ToArray(),
                            transportTransaction,
                            deliveryAttempts)).ConfigureAwait(false);

                        if (errorHandlerResult == ErrorHandleResult.RetryRequired)
                        {
                            await ReturnMessageToQueue(nativeMessage).ConfigureAwait(false);

                            return false;
                        }
                    }
                    catch (Exception onErrorEx)
                    {
                        criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`", onErrorEx);

                        await ReturnMessageToQueue(nativeMessage).ConfigureAwait(false);

                        return false;
                    }
                }

                if (messageContextCancellationTokenSource.IsCancellationRequested)
                {
                    await ReturnMessageToQueue(nativeMessage).ConfigureAwait(false);

                    return false;
                }

                return true;
            }
        }

        async Task ReturnMessageToQueue(Message message)
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

        int GetDeliveryAttempts(string nativeMessageId)
        {
            deliveryAttemptsStorage.RecordFailureInfoForMessage(nativeMessageId);

            deliveryAttemptsStorage.TryGetFailureInfoForMessage(nativeMessageId, out var attempts);

            return attempts;
        }

        static bool IsMessageExpired(Message receivedMessage, Dictionary<string, string> headers, string messageId, TimeSpan clockOffset)
        {
            if (!headers.TryGetValue(TransportHeaders.TimeToBeReceived, out var rawTtbr))
            {
                return false;
            }

            headers.Remove(TransportHeaders.TimeToBeReceived);
            var timeToBeReceived = TimeSpan.Parse(rawTtbr);
            if (timeToBeReceived == TimeSpan.MaxValue)
            {
                return false;
            }

            var sentDateTime = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("SentTimestamp", clockOffset);
            var expiresAt = sentDateTime + timeToBeReceived;
            var utcNow = DateTime.UtcNow;
            if (expiresAt > utcNow)
            {
                return false;
            }

            // Message has expired.
            Logger.Info($"Discarding expired message with Id {messageId}, expired {utcNow - expiresAt} ago at {expiresAt} utc.");
            return true;
        }

        async Task DeleteMessage(Message message, string s3BodyKey)
        {
            try
            {
                // should not be cancelled
                await sqsClient.DeleteMessageAsync(inputQueueUrl, message.ReceiptHandle, CancellationToken.None).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(s3BodyKey))
                {
                    Logger.Info($"Message body data with key '{s3BodyKey}' will be aged out by the S3 lifecycle policy when the TTL expires.");
                }
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Error($"Failed to delete message with native ID '{message.MessageId}' because the handler execution time exceeded the visibility timeout. Increase the length of the timeout on the queue. The message was returned to the queue.", ex);

                messagesToBeDeletedStorage.RecordFailureInfoForMessage(message.MessageId);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to delete message with native ID '{message.MessageId}'. The message will be returned to the queue when the visibility timeout expires.", ex);

                messagesToBeDeletedStorage.RecordFailureInfoForMessage(message.MessageId);
            }
        }

        async Task MovePoisonMessageToErrorQueue(Message message)
        {
            try
            {
                // Ok to use LINQ here since this is not really a hot path
                var messageAttributeValues = message.MessageAttributes
                    .ToDictionary(pair => pair.Key, messageAttribute => messageAttribute.Value);

                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = errorQueueUrl,
                    MessageBody = message.Body,
                    MessageAttributes = messageAttributeValues
                }, CancellationToken.None).ConfigureAwait(false);
                // The Attributes on message are read-only attributes provided by SQS
                // and can't be re-sent. Unfortunately all the SQS metadata
                // such as SentTimestamp is reset with this send.
            }
            catch (Exception ex)
            {
                Logger.Error($"Error moving poison message to error queue at url {errorQueueUrl}. Moving back to input queue.", ex);
                try
                {
                    await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                    {
                        QueueUrl = inputQueueUrl,
                        ReceiptHandle = message.ReceiptHandle,
                        VisibilityTimeout = 0
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception changeMessageVisibilityEx)
                {
                    Logger.Warn($"Error returning poison message back to input queue at url {inputQueueUrl}. Poison message will become available at the input queue again after the visibility timeout expires.", changeMessageVisibilityEx);
                }

                return;
            }

            try
            {
                await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = inputQueueUrl,
                    ReceiptHandle = message.ReceiptHandle
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error removing poison message from input queue {inputQueueUrl}. This may cause duplicate poison messages in the error queue for this endpoint.", ex);
            }

            // If there is a message body in S3, simply leave it there
        }

        readonly FailureInfoStorage deliveryAttemptsStorage = new FailureInfoStorage(1_000);
        readonly FailureInfoStorage messagesToBeDeletedStorage = new FailureInfoStorage(1_000);
        List<Task> pumpTasks;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        Func<MessageContext, Task> onMessage;
        SemaphoreSlim maxConcurrencySemaphore;
        string inputQueueUrl;
        string errorQueueUrl;
        int maxConcurrency;
        TransportConfiguration configuration;
        IAmazonS3 s3Client;
        IAmazonSQS sqsClient;
        QueueCache queueCache;
        int numberOfMessagesToFetch;
        ReceiveMessageRequest receiveMessagesRequest;
        CriticalError criticalError;

        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    }
}