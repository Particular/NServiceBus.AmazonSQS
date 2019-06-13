namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using AmazonSQS;
    using Extensibility;
    using Logging;
    using SimpleJson;
    using Transport;

    class MessagePump : IPushMessages
    {
        public MessagePump(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
            awsEndpointUrl = sqsClient.Config.DetermineServiceURL();
        }

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            this.criticalError = criticalError;

            queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(settings.InputQueue, configuration))
                .ConfigureAwait(false);
            errorQueueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(settings.ErrorQueue, configuration))
                .ConfigureAwait(false);

            if (configuration.IsDelayedDeliveryEnabled)
            {
                var delayedDeliveryQueueName = settings.InputQueue + TransportConfiguration.DelayedDeliveryQueueSuffix;
                delayedDeliveryQueueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(delayedDeliveryQueueName, configuration))
                    .ConfigureAwait(false);

                var queueAttributes = await GetQueueAttributesFromDelayedDeliveryQueueWithRetriesToWorkaroundSDKIssue()
                    .ConfigureAwait(false);

                if (queueAttributes.DelaySeconds < configuration.DelayedDeliveryQueueDelayTime)
                {
                    throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Delivery Delay less than {TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}.");
                }

                if (queueAttributes.MessageRetentionPeriod < (int)TransportConfiguration.DelayedDeliveryQueueMessageRetentionPeriod.TotalSeconds)
                {
                    throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Message Retention Period less than {TransportConfiguration.DelayedDeliveryQueueMessageRetentionPeriod}.");
                }

                if (queueAttributes.Attributes.ContainsKey("RedrivePolicy"))
                {
                    throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Redrive Policy enabled.");
                }
            }

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
                AttributeNames = new List<string> {"SentTimestamp"},
                MessageAttributeNames = new List<string> {Headers.MessageId}
            };

            maxConcurrencySemaphore = new SemaphoreSlim(maxConcurrency);
            pumpTasks = new List<Task>(numberOfPumps);

            for (var i = 0; i < numberOfPumps; i++)
            {
                pumpTasks.Add(Task.Run(() => ConsumeMessages(cancellationTokenSource.Token), CancellationToken.None));
            }

            if (configuration.IsDelayedDeliveryEnabled)
            {
                var receiveDelayedMessagesRequest = new ReceiveMessageRequest
                {
                    MaxNumberOfMessages = 10,
                    QueueUrl = delayedDeliveryQueueUrl,
                    WaitTimeSeconds = 20,
                    AttributeNames = new List<string> {"MessageDeduplicationId", "SentTimestamp", "ApproximateFirstReceiveTimestamp", "ApproximateReceiveCount"},
                    MessageAttributeNames = new List<string> {"All"}
                };

                pumpTasks.Add(Task.Run(() => ConsumeDelayedMessages(receiveDelayedMessagesRequest, cancellationTokenSource.Token), CancellationToken.None));
            }
        }

        public async Task Stop()
        {
            cancellationTokenSource?.Cancel();

            await Task.WhenAll(pumpTasks).ConfigureAwait(false);
            pumpTasks?.Clear();

            while (maxConcurrencySemaphore.CurrentCount != maxConcurrency)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            cancellationTokenSource?.Dispose();
            maxConcurrencySemaphore?.Dispose();
        }

        async Task ConsumeDelayedMessages(ReceiveMessageRequest request, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var receivedMessages = await sqsClient.ReceiveMessageAsync(request, token).ConfigureAwait(false);
                    var clockCorrection = CorrectClockSkew.GetClockCorrectionForEndpoint(awsEndpointUrl);

                    foreach (var receivedMessage in receivedMessages.Messages)
                    {
                        long delaySeconds = 0;

                        if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.DelaySeconds, out var delayAttribute))
                        {
                            long.TryParse(delayAttribute.StringValue, out delaySeconds);
                        }

                        var sent = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("SentTimestamp", clockCorrection);
                        var received = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("ApproximateFirstReceiveTimestamp", clockCorrection);

                        if (Convert.ToInt32(receivedMessage.Attributes["ApproximateReceiveCount"]) > 1)
                        {
                            received = DateTime.UtcNow;
                        }

                        var elapsed = received - sent;

                        var remainingDelay = delaySeconds - (long)elapsed.TotalSeconds;

                        SendMessageRequest sendMessageRequest;

                        if (remainingDelay > configuration.DelayedDeliveryQueueDelayTime)
                        {
                            sendMessageRequest = new SendMessageRequest(delayedDeliveryQueueUrl, receivedMessage.Body)
                            {
                                MessageAttributes =
                                {
                                    [TransportHeaders.DelaySeconds] = new MessageAttributeValue
                                    {
                                        StringValue = remainingDelay.ToString(),
                                        DataType = "String"
                                    }
                                }
                            };

                            var deduplicationId = receivedMessage.Attributes["MessageDeduplicationId"];

                            // this is only here for acceptance testing purpose. In real prod code this is always false.
                            if (configuration.DelayedDeliveryQueueDelayTime < TransportConfiguration.AwsMaximumQueueDelayTime)
                            {
                                deduplicationId = Guid.NewGuid().ToString();
                            }

                            sendMessageRequest.MessageDeduplicationId = sendMessageRequest.MessageGroupId = deduplicationId;
                        }
                        else
                        {
                            sendMessageRequest = new SendMessageRequest(queueUrl, receivedMessage.Body);

                            if (remainingDelay > 0)
                            {
                                sendMessageRequest.DelaySeconds = Convert.ToInt32(remainingDelay);
                            }
                        }

                        try
                        {
                            await sqsClient.SendMessageAsync(sendMessageRequest, CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug("ConsumeDelayedMessages -> SendMessageAsync failed", ex);

                            await sqsClient.ChangeMessageVisibilityAsync(request.QueueUrl, receivedMessage.ReceiptHandle, 0, CancellationToken.None)
                                .ConfigureAwait(false);

                            continue;
                        }

                        try
                        {
                            await sqsClient.DeleteMessageAsync(delayedDeliveryQueueUrl, receivedMessage.ReceiptHandle, CancellationToken.None)
                                .ConfigureAwait(false);
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

                        ProcessMessage(receivedMessage, token).Ignore();
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

        async Task ProcessMessage(Message receivedMessage, CancellationToken token)
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

                    transportMessage = SimpleJson.DeserializeObject<TransportMessage>(receivedMessage.Body);
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

                    await MovePoisonMessageToErrorQueue(receivedMessage, messageId).ConfigureAwait(false);
                    return;
                }

                if (!IsMessageExpired(receivedMessage, transportMessage.Headers, messageId, CorrectClockSkew.GetClockCorrectionForEndpoint(awsEndpointUrl)))
                {
                    // here we also want to use the native message id because the core demands it like that
                    await ProcessMessageWithInMemoryRetries(transportMessage.Headers, nativeMessageId, messageBody, token).ConfigureAwait(false);
                }

                // Always delete the message from the queue.
                // If processing failed, the onError handler will have moved the message
                // to a retry queue.
                await DeleteMessageAndBodyIfRequired(receivedMessage, transportMessage.S3BodyKey).ConfigureAwait(false);
            }
            finally
            {
                maxConcurrencySemaphore.Release();
            }
        }

        async Task ProcessMessageWithInMemoryRetries(Dictionary<string, string> headers, string nativeMessageId, byte[] body, CancellationToken token)
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
                            nativeMessageId,
                            new Dictionary<string, string>(headers),
                            body,
                            transportTransaction,
                            messageContextCancellationTokenSource,
                            new ContextBag());

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

        async Task DeleteMessageAndBodyIfRequired(Message message, string s3BodyKey)
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

            if (!string.IsNullOrEmpty(s3BodyKey))
            {
                Logger.Info($"Message body data with key '{s3BodyKey}' will be aged out by the S3 lifecycle policy when the TTL expires.");
            }
        }

        async Task MovePoisonMessageToErrorQueue(Message message, string messageId)
        {
            try
            {
                await sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = errorQueueUrl,
                    MessageBody = message.Body,
                    MessageAttributes =
                    {
                        [Headers.MessageId] = new MessageAttributeValue
                        {
                            StringValue = messageId,
                            DataType = "String"
                        }
                    }
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

        async Task<GetQueueAttributesResponse> GetQueueAttributesFromDelayedDeliveryQueueWithRetriesToWorkaroundSDKIssue()
        {
            var attributeNames = new List<string>
            {
                "DelaySeconds",
                "MessageRetentionPeriod",
                "RedrivePolicy"
            };

            GetQueueAttributesResponse queueAttributes = null;

            for (var i = 0; i < 4; i++)
            {
                queueAttributes = await sqsClient.GetQueueAttributesAsync(delayedDeliveryQueueUrl, attributeNames)
                    .ConfigureAwait(false);

                if (queueAttributes.DelaySeconds != 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(i))
                    .ConfigureAwait(false);
            }

            return queueAttributes;
        }

        CancellationTokenSource cancellationTokenSource;
        List<Task> pumpTasks;
        Func<ErrorContext, Task<ErrorHandleResult>> onError;
        Func<MessageContext, Task> onMessage;
        SemaphoreSlim maxConcurrencySemaphore;
        string queueUrl;
        string delayedDeliveryQueueUrl;
        string errorQueueUrl;
        int maxConcurrency;
        TransportConfiguration configuration;
        IAmazonS3 s3Client;
        IAmazonSQS sqsClient;
        QueueUrlCache queueUrlCache;
        int numberOfMessagesToFetch;
        ReceiveMessageRequest receiveMessagesRequest;
        CriticalError criticalError;
        string awsEndpointUrl;

        static readonly TransportTransaction transportTransaction = new TransportTransaction();
        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    }
}
