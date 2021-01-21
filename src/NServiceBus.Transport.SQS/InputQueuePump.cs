
namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensibility;
    using Extensions;
    using Logging;
    using SimpleJson;
    using static TransportHeaders;


    class InputQueuePump : IMessageReceiver
    {
        public InputQueuePump(ReceiveSettings settings, IAmazonSQS sqsClient, QueueCache queueCache,
            S3Settings s3Settings, SubscriptionManager subscriptionManager,
            Action<string, Exception> criticalErrorAction)
        {
            this.settings = settings;
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
            this.s3Settings = s3Settings;
            this.criticalErrorAction = criticalErrorAction;
            awsEndpointUrl = sqsClient.Config.DetermineServiceURL();
            Id = settings.Id;
            Subscriptions = subscriptionManager;
        }

        public async Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError)
        {
            inputQueueUrl = await queueCache.GetQueueUrl(settings.ReceiveAddress)
                .ConfigureAwait(false);
            errorQueueUrl = await queueCache.GetQueueUrl(settings.ErrorQueue)
                .ConfigureAwait(false);

            maxConcurrency = limitations.MaxConcurrency;


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
                    _logger.Warn("Multiple queue purges within 60 seconds are not permitted by SQS.", ex);
                }
                catch (Exception ex)
                {
                    _logger.Error("Exception thrown from PurgeQueue.", ex);
                    throw;
                }
            }

            this.onMessage = onMessage;
            this.onError = onError;
        }

        public Task StartReceive()
        {
            if (tokenSource != null)
            {
                return Task.CompletedTask; //Receiver already started.
            }

            tokenSource = new CancellationTokenSource();
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
                MessageAttributeNames = new List<string> { "*" }
            };

            maxConcurrencySemaphore = new SemaphoreSlim(maxConcurrency);
            pumpTasks = new List<Task>(numberOfPumps);

            for (var i = 0; i < numberOfPumps; i++)
            {
                pumpTasks.Add(Task.Run(() => ConsumeMessages(tokenSource.Token), CancellationToken.None));
            }

            return Task.CompletedTask;
        }

        public async Task StopReceive()
        {
            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();

            if (pumpTasks != null)
            {
                await Task.WhenAll(pumpTasks).ConfigureAwait(false);
                pumpTasks = null;
            }

            while (maxConcurrencySemaphore.CurrentCount != maxConcurrency)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            tokenSource.Dispose();
            maxConcurrencySemaphore?.Dispose();
            tokenSource = null;
        }

        public ISubscriptionManager Subscriptions { get; }
        public string Id { get; }

        async Task ConsumeMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var receivedMessages = await sqsClient.ReceiveMessageAsync(receiveMessagesRequest, token).ConfigureAwait(false);

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
                    _logger.Error("Exception thrown when consuming messages", ex);
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

                        if (receivedMessage.MessageAttributes.TryGetValue(S3BodyKey, out var s3BodyKey))
                        {
                            headers.Add(S3BodyKey, s3BodyKey.StringValue);
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
                        transportMessage = SimpleJson.DeserializeObject<TransportMessage>(receivedMessage.Body);
                    }

                    messageBody = await transportMessage.RetrieveBody(messageId, s3Settings, token).ConfigureAwait(false);
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
                        _logger.Warn(logMessage, exception);
                    }
                    else
                    {
                        _logger.Warn(logMessage);
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
                try
                {
                    // set the native message on the context for advanced usage scenario's
                    var context = new ContextBag();
                    context.Set(nativeMessage);
                    // We add it to the transport transaction to make it available in dispatching scenario's so we copy over message attributes when moving messages to the error/audit queue
                    TransportTransaction.Set(nativeMessage);
                    TransportTransaction.Set("IncomingMessageId", headers[Headers.MessageId]);

                    using (var messageContextCancellationTokenSource = new CancellationTokenSource())
                    {
                        var messageContext = new MessageContext(
                            nativeMessageId,
                            new Dictionary<string, string>(headers),
                            body,
                            TransportTransaction,
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
                            TransportTransaction,
                            immediateProcessingAttempts)).ConfigureAwait(false);
                    }
                    catch (Exception onErrorEx)
                    {
                        criticalErrorAction($"Failed to execute recoverability policy for message with native ID: `{nativeMessageId}`", onErrorEx);
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

            var sentAt = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("SentTimestamp", clockOffset);
            var expiresAt = sentAt + timeToBeReceived;
            var now = DateTimeOffset.UtcNow;
            if (expiresAt > now)
            {
                return false;
            }

            // Message has expired.
            _logger.Info($"Discarding expired message with Id {messageId}, expired {now - expiresAt} ago at {expiresAt} utc.");
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
                _logger.Info($"Message receipt handle {message.ReceiptHandle} no longer valid.", ex);
                return; // if another receiver fetches the data from S3
            }

            if (!string.IsNullOrEmpty(s3BodyKey))
            {
                _logger.Info($"Message body data with key '{s3BodyKey}' will be aged out by the S3 lifecycle policy when the TTL expires.");
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
                _logger.Error($"Error moving poison message to error queue at url {errorQueueUrl}. Moving back to input queue.", ex);
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
                    _logger.Warn($"Error returning poison message back to input queue at url {inputQueueUrl}. Poison message will become available at the input queue again after the visibility timeout expires.", changeMessageVisibilityEx);
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
                _logger.Warn($"Error removing poison message from input queue {inputQueueUrl}. This may cause duplicate poison messages in the error queue for this endpoint.", ex);
            }

            // If there is a message body in S3, simply leave it there
        }

        List<Task> pumpTasks;
        OnError onError;
        OnMessage onMessage;
        SemaphoreSlim maxConcurrencySemaphore;
        string inputQueueUrl;
        string errorQueueUrl;
        int maxConcurrency;

        readonly ReceiveSettings settings;
        readonly IAmazonSQS sqsClient;
        readonly QueueCache queueCache;
        readonly S3Settings s3Settings;
        readonly Action<string, Exception> criticalErrorAction;
        readonly string awsEndpointUrl;

        int numberOfMessagesToFetch;
        ReceiveMessageRequest receiveMessagesRequest;
        CancellationTokenSource tokenSource;

        static readonly TransportTransaction TransportTransaction = new TransportTransaction();
        static ILog _logger = LogManager.GetLogger(typeof(MessagePump));
    }
}
