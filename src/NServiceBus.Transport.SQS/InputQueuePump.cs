namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensibility;
    using Extensions;
    using Logging;
    using SimpleJson;
    using static TransportHeaders;

    class InputQueuePump
    {
        public InputQueuePump(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueCache queueCache)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
            awsEndpointUrl = sqsClient.Config.DetermineServiceURL();
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
                MessageAttributeNames = new List<string> { "*" },
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

                        _ = ProcessMessage(receivedMessage, maxConcurrencySemaphore, token);
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

                    // When the MessageTypeFullName attribute is available, we're assuming native integration
                    if (receivedMessage.MessageAttributes.TryGetValue(MessageTypeFullName, out var enclosedMessageType))
                    {
                        var headers = new Dictionary<string, string>
                        {
                            {Headers.MessageId, messageId},
                            {Headers.EnclosedMessageTypes, enclosedMessageType.StringValue},
                            {MessageTypeFullName, enclosedMessageType.StringValue} // we're copying over the value of the native message attribute into the headers, converting this into a nsb message
                        };

                        if (receivedMessage.MessageAttributes.TryGetValue(S3BodyKey, out var s3bodyKey))
                        {
                            headers.Add(S3BodyKey, s3bodyKey.StringValue);
                        }

                        transportMessage = new TransportMessage
                        {
                            Headers = headers,
                            S3BodyKey = s3bodyKey?.StringValue,
                            Body = receivedMessage.Body
                        };
                    }
                    else
                    {
                        transportMessage = SimpleJson.DeserializeObject<TransportMessage>(receivedMessage.Body);
                    }

                    messageBody = await transportMessage.RetrieveBody(s3Client, configuration, token).ConfigureAwait(false);
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

                if (isPoisonMessage || messageBody == null || transportMessage == null)
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

                if (!IsMessageExpired(receivedMessage, transportMessage.Headers, messageId, CorrectClockSkew.GetClockCorrectionForEndpoint(awsEndpointUrl)))
                {
                    // here we also want to use the native message id because the core demands it like that
                    await ProcessMessageWithInMemoryRetries(transportMessage.Headers, nativeMessageId, messageBody, receivedMessage, token).ConfigureAwait(false);
                }

                // Always delete the message from the queue.
                // If processing failed, the onError handler will have moved the message
                // to a retry queue.
                await DeleteMessage(receivedMessage, transportMessage.S3BodyKey).ConfigureAwait(false);
            }
            finally
            {
                processingSemaphoreSlim.Release();
            }
        }

        async Task ProcessMessageWithInMemoryRetries(Dictionary<string, string> headers, string nativeMessageId, byte[] body, Message nativeMessage, CancellationToken token)
        {
            var immediateProcessingAttempts = 0;
            var messageProcessedOk = false;
            var errorHandled = false;

            while (!errorHandled && !messageProcessedOk)
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
                    using (var messageContextCancellationTokenSource = new CancellationTokenSource())
                    {
                        var messageContext = new MessageContext(
                            nativeMessageId,
                            new Dictionary<string, string>(headers),
                            body,
                            transportTransaction,
                            messageContextCancellationTokenSource,
                            context);

                        await onMessage(messageContext).ConfigureAwait(false);

                        messageProcessedOk = !messageContextCancellationTokenSource.IsCancellationRequested;
                    }
                }
                catch (Exception ex)
                    when (!(ex is OperationCanceledException && token.IsCancellationRequested))
                {
                    immediateProcessingAttempts++;
                    var errorHandlerResult = ErrorHandleResult.RetryRequired;

                    try
                    {
                        errorHandlerResult = await onError(new ErrorContext(ex,
                            new Dictionary<string, string>(headers),
                            nativeMessageId,
                            body,
                            transportTransaction,
                            immediateProcessingAttempts)).ConfigureAwait(false);
                    }
                    catch (Exception onErrorEx)
                    {
                        criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`", onErrorEx);
                    }

                    errorHandled = errorHandlerResult == ErrorHandleResult.Handled;
                }
            }
        }

        static bool IsMessageExpired(Message receivedMessage, Dictionary<string, string> headers, string messageId, TimeSpan clockOffset)
        {
            if (!headers.TryGetValue(TimeToBeReceived, out var rawTtbr))
            {
                return false;
            }

            headers.Remove(TimeToBeReceived);
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
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Info($"Message receipt handle {message.ReceiptHandle} no longer valid.", ex);
                return; // if another receiver fetches the data from S3
            }

            if (!string.IsNullOrEmpty(s3BodyKey))
            {
                Logger.Info($"Message body data with key '{s3BodyKey}' will be aged out by the S3 lifecycle policy when the TTL expires.");
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
        string awsEndpointUrl;

        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    }
}