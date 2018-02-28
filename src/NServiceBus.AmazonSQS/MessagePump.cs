namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
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

    class MessagePump : IPushMessages
    {
        public MessagePump(ConnectionConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache, bool isDelayedDeliveryEnabled)
        {
            this.isDelayedDeliveryEnabled = isDelayedDeliveryEnabled;
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
        }

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(settings.InputQueue, configuration))
                .ConfigureAwait(false);
            errorQueueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(settings.ErrorQueue, configuration))
                .ConfigureAwait(false);
            delayedDeliveryQueueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(settings.InputQueue + "-delay.fifo", configuration))
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
                    await sqsClient.PurgeQueueAsync(queueUrl, CancellationToken.None).ConfigureAwait(false);
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
        }

        public void Start(PushRuntimeSettings limitations)
        {
            cancellationTokenSource = new CancellationTokenSource();
            maxConcurrency = limitations.MaxConcurrency;

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
                QueueUrl = queueUrl,
                WaitTimeSeconds = 20,
                AttributeNames = new List<string>
                {
                    "SentTimestamp"
                }
            };

            maxConcurrencySempahore = new SemaphoreSlim(maxConcurrency);
            pumpTasks = new List<Task>(numberOfPumps);

            for (var i = 0; i < numberOfPumps; i++)
            {
                pumpTasks.Add(Task.Run(() => ConsumeMessages(cancellationTokenSource.Token), CancellationToken.None));
            }

            if (isDelayedDeliveryEnabled)
            {
                var receiveDelayedMessagesRequest = new ReceiveMessageRequest
                {
                    MaxNumberOfMessages = 10,
                    QueueUrl = delayedDeliveryQueueUrl,
                    WaitTimeSeconds = 20,
                    AttributeNames = new List<string> { "MessageDeduplicationId" }
                };

                pumpTasks.Add(Task.Run(() => ConsumeDelayedMessages(receiveDelayedMessagesRequest, cancellationTokenSource.Token), CancellationToken.None));
            }
        }

        public async Task Stop()
        {
            cancellationTokenSource?.Cancel();

            await Task.WhenAll(pumpTasks).ConfigureAwait(false);

            pumpTasks?.Clear();
            cancellationTokenSource?.Dispose();
            maxConcurrencySempahore?.Dispose();
        }

        async Task ConsumeDelayedMessages(ReceiveMessageRequest request, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var receivedMessages = await sqsClient.ReceiveMessageAsync(request, token).ConfigureAwait(false);

                    foreach (var receivedMessage in receivedMessages.Messages)
                    {
                        // should we be more defensive here?
                        var delayAttribute = receivedMessage.MessageAttributes[TransportHeaders.DelayDueTime];
                        var dueTime = DateTimeExtensions.ToUtcDateTime(delayAttribute.StringValue);
                        // TODO: add AWSConfigs.ClockOffset for clock skew (verify how it works)
                        var utcNow = DateTime.UtcNow;

                        var delaySeconds = dueTime - utcNow;
                        SendMessageRequest sendMessageRequest;

                        if (delaySeconds > awsMaxDelayInMinutes)
                        {
                            sendMessageRequest = new SendMessageRequest(delayedDeliveryQueueUrl, receivedMessage.Body)
                            {
                                MessageAttributes =
                                    {
                                        [TransportHeaders.DelayDueTime] = delayAttribute
                                    }
                            };
                            sendMessageRequest.MessageDeduplicationId = sendMessageRequest.MessageGroupId = receivedMessage.Attributes["MessageDeduplicationId"];
                        }
                        else
                        {
                            sendMessageRequest = new SendMessageRequest(queueUrl, receivedMessage.Body)
                            {
                                DelaySeconds = (int)delaySeconds.TotalSeconds
                            };
                        }

                        try
                        {
                            // should we allow to cancel this?
                            await sqsClient.SendMessageAsync(sendMessageRequest, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // change visibility here?
                            Logger.Error("BOOOM!", ex);
                        }

                        try
                        {
                            // should not be cancelled
                            await sqsClient.DeleteMessageAsync(delayedDeliveryQueueUrl, receivedMessage.ReceiptHandle, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (ReceiptHandleIsInvalidException ex)
                        {
                            Logger.Info($"Message receipt handle {receivedMessage.ReceiptHandle} no longer valid.", ex);
                        }

                    }
                }
                catch (OperationCanceledException)
                {
                    // ignore for graceful shutdown
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming delayed messages", ex);
                }
            }
        }

        async Task ConsumeMessages(CancellationToken token)
        {
            // cached and reused per receive loop
            var concurrentReceiveOperations = new List<Task>(numberOfMessagesToFetch);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var receivedMessages = await sqsClient.ReceiveMessageAsync(receiveMessagesRequest, token).ConfigureAwait(false);

                    ProcessMessages(receivedMessages.Messages, concurrentReceiveOperations, token);

                    await Task.WhenAll(concurrentReceiveOperations).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore for graceful shutdown
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming messages", ex);
                }
                finally
                {
                    concurrentReceiveOperations.Clear();
                }
            } // while
        }

        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        // ReSharper disable once SuggestBaseTypeForParameter
        void ProcessMessages(List<Message> receivedMessages, List<Task> concurrentReceiveOperations, CancellationToken token)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var receivedMessage in receivedMessages)
            {
                concurrentReceiveOperations.Add(ProcessMessage(receivedMessage, token));
            }
        }

        async Task ProcessMessage(Message receivedMessage, CancellationToken token)
        {
            try
            {
                await maxConcurrencySempahore.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // shutting, semaphore doesn't need to be released because it was never acquired
                return;
            }

            try
            {
                IncomingMessage incomingMessage = null;
                TransportMessage transportMessage = null;

                var isPoisonMessage = false;
                try
                {
                    transportMessage = JsonConvert.DeserializeObject<TransportMessage>(receivedMessage.Body);

                    incomingMessage = await transportMessage.ToIncomingMessage(s3Client, configuration, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // shutting down
                    return;
                }
                catch (Exception ex)
                {
                    // Can't deserialize. This is a poison message
                    Logger.Warn($"Treating message with SQS Message Id {receivedMessage.MessageId} as a poison message due to exception {ex}. Moving to error queue.");
                    isPoisonMessage = true;
                }

                if (incomingMessage == null || transportMessage == null)
                {
                    Logger.Warn($"Treating message with SQS Message Id {receivedMessage.MessageId} as a poison message because it could not be converted to an IncomingMessage. Moving to error queue.");
                    isPoisonMessage = true;
                }

                if (isPoisonMessage)
                {
                    await MovePoisonMessageToErrorQueue(receivedMessage).ConfigureAwait(false);
                    return;
                }

                if (!IsMessageExpired(receivedMessage, incomingMessage))
                {
                    await ProcessMessageWithInMemoryRetries(incomingMessage, token).ConfigureAwait(false);
                }

                // Always delete the message from the queue.
                // If processing failed, the onError handler will have moved the message
                // to a retry queue.
                await DeleteMessageAndBodyIfRequired(receivedMessage, transportMessage, token).ConfigureAwait(false);
            }
            finally
            {
                maxConcurrencySempahore.Release();
            }
        }

        async Task ProcessMessageWithInMemoryRetries(IncomingMessage incomingMessage, CancellationToken token)
        {
            var immediateProcessingAttempts = 0;
            var messageProcessedOk = false;
            var errorHandled = false;

            while (!errorHandled && !messageProcessedOk)
            {
                try
                {
                    using (var messageContextCancellationTokenSource = new CancellationTokenSource())
                    {
                        var messageContext = new MessageContext(
                            incomingMessage.MessageId,
                            incomingMessage.Headers,
                            incomingMessage.Body,
                            transportTransaction,
                            messageContextCancellationTokenSource,
                            contextBag);

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
                            incomingMessage.Headers,
                            incomingMessage.MessageId,
                            incomingMessage.Body,
                            transportTransaction,
                            immediateProcessingAttempts)).ConfigureAwait(false);
                    }
                    catch (Exception onErrorEx)
                    {
                        Logger.Error("Exception thrown from error handler", onErrorEx);
                    }
                    errorHandled = errorHandlerResult == ErrorHandleResult.Handled;
                }
            }
        }

        static bool IsMessageExpired(Message receivedMessage, IncomingMessage incomingMessage)
        {
            if (!incomingMessage.Headers.TryGetValue(TransportHeaders.TimeToBeReceived, out var rawTtbr))
            {
                return false;
            }

            incomingMessage.Headers.Remove(TransportHeaders.TimeToBeReceived);
            var timeToBeReceived = TimeSpan.Parse(rawTtbr);
            if (timeToBeReceived == TimeSpan.MaxValue)
            {
                return false;
            }

            var sentDateTime = receivedMessage.GetSentDateTime();
            var utcNow = DateTime.UtcNow;
            var expiresAt = sentDateTime + timeToBeReceived;
            if (expiresAt > utcNow)
            {
                return false;
            }
            // Message has expired.
            Logger.Info($"Discarding expired message with Id {incomingMessage.MessageId}, expired {utcNow - expiresAt} ago at {expiresAt} utc.");
            return true;
        }

        async Task DeleteMessageAndBodyIfRequired(Message message, TransportMessage transportMessage, CancellationToken token)
        {
            try
            {
                // should not be cancelled
                await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, CancellationToken.None).ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Info($"Message receipt handle {message.ReceiptHandle} no longer valid.", ex);
                return; // if another receiver fetches the data from S3
            }

            if (transportMessage != null)
            {
                if (!string.IsNullOrEmpty(transportMessage.S3BodyKey))
                {
                    try
                    {
                        await s3Client.DeleteObjectAsync(
                            new DeleteObjectRequest
                            {
                                BucketName = configuration.S3BucketForLargeMessages,
                                Key = transportMessage.S3BodyKey
                            },
                            token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // If deleting the message body from S3 fails, we don't
                        // want the exception to make its way through to the _endProcessMessage below,
                        // as the message has been successfully processed and deleted from the SQS queue
                        // and effectively doesn't exist anymore.
                        // It doesn't really matter, as S3 is configured to delete message body data
                        // automatically after a certain period of time.
                        Logger.Warn("Couldn't delete message body from S3. Message body data will be aged out by the S3 lifecycle policy when the TTL expires.", ex);
                    }
                }
            }
            else
            {
                Logger.Warn("Couldn't delete message body from S3 because the TransportMessage was null. Message body data will be aged out by the S3 lifecycle policy when the TTL expires.");
            }
        }

        async Task MovePoisonMessageToErrorQueue(Message message)
        {
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = errorQueueUrl,
                    MessageBody = message.Body
                }, CancellationToken.None).ConfigureAwait(false);
                // The MessageAttributes on message are read-only attributes provided by SQS
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
                        QueueUrl = queueUrl,
                        ReceiptHandle = message.ReceiptHandle,
                        VisibilityTimeout = 0
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception changeMessageVisibilityEx)
                {
                    Logger.Warn($"Error returning poison message back to input queue at url {queueUrl}. Poison message will become available at the input queue again after the visibility timeout expires.", changeMessageVisibilityEx);
                }
                return;
            }

            try
            {
                await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                {
                    QueueUrl = queueUrl,
                    ReceiptHandle = message.ReceiptHandle
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error removing poison message from input queue {queueUrl}. This may cause duplicate poison messages in the error queue for this endpoint.", ex);
            }
            // If there is a message body in S3, simply leave it there
        }

        CancellationTokenSource cancellationTokenSource;
        List<Task> pumpTasks;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        Func<MessageContext, Task> onMessage;
        SemaphoreSlim maxConcurrencySempahore;
        string queueUrl;
        string delayedDeliveryQueueUrl;
        string errorQueueUrl;
        int maxConcurrency;
        ConnectionConfiguration configuration;
        IAmazonS3 s3Client;
        IAmazonSQS sqsClient;
        QueueUrlCache queueUrlCache;
        int numberOfMessagesToFetch;
        ReceiveMessageRequest receiveMessagesRequest;
        bool isDelayedDeliveryEnabled;
        static readonly TransportTransaction transportTransaction = new TransportTransaction();
        static readonly ContextBag contextBag = new ContextBag();
        static readonly TimeSpan awsMaxDelayInMinutes = TimeSpan.FromMinutes(15); // should be somewhere else

        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    }
}