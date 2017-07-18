namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

    class SqsMessagePump : IPushMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public SqsQueueUrlCache SqsQueueUrlCache { get; set; }

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            _queueUrl = SqsQueueUrlCache.GetQueueUrl(SqsQueueNameHelper.GetSqsQueueName(settings.InputQueue, ConnectionConfiguration));

            if (settings.PurgeOnStartup)
            {
                // SQS only allows purging a queue once every 60 seconds or so.
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown.
                // This will happen if you are trying to start an endpoint twice or more
                // in that time.
                try
                {
                    await SqsClient.PurgeQueueAsync(_queueUrl).ConfigureAwait(false);
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

            _onMessage = onMessage;
            _onError = onError;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _concurrencyLevel = limitations.MaxConcurrency;
            _maxConcurrencySempahore = new SemaphoreSlim(_concurrencyLevel);
            _consumerTasks = new List<Task>();

            for (var i = 0; i < _concurrencyLevel; i++)
            {
                _consumerTasks.Add(ConsumeMessages());
            }
        }

        public async Task Stop()
        {
            _cancellationTokenSource?.Cancel();

            try
            {
                await Task.WhenAll(_consumerTasks.ToArray()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Silently swallow OperationCanceledException
                // These are expected to be thrown when _cancellationTokenSource
                // is Cancel()ed above.
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        async Task ConsumeMessages()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                        {
                            MaxNumberOfMessages = 10,
                            QueueUrl = _queueUrl,
                            WaitTimeSeconds = 20,
                            AttributeNames = new List<String>
                            {
                                "SentTimestamp"
                            }
                        },
                        _cancellationTokenSource.Token).ConfigureAwait(false);

                    var tasks = receiveResult.Messages.Select(async message =>
                    {
                        IncomingMessage incomingMessage = null;
                        SqsTransportMessage sqsTransportMessage = null;
                        var transportTransaction = new TransportTransaction();
                        var contextBag = new ContextBag();

                        var messageProcessedOk = false;
                        var messageExpired = false;
                        var isPoisonMessage = false;
                        var errorHandled = false;

                        try
                        {
                            sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

                            incomingMessage = await sqsTransportMessage.ToIncomingMessage(S3Client,
                                ConnectionConfiguration,
                                _cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Can't deserialize. This is a poison message
                            Logger.Warn($"Deleting poison message with SQS Message Id {message.MessageId} due to exception {ex}");
                            isPoisonMessage = true;
                        }

                        if (incomingMessage == null || sqsTransportMessage == null)
                        {
                            Logger.Warn($"Deleting poison message with SQS Message Id {message.MessageId}");
                            isPoisonMessage = true;
                        }

                        if (isPoisonMessage)
                        {
                            await DeleteMessage(SqsClient,
                                S3Client,
                                message,
                                sqsTransportMessage,
                                incomingMessage).ConfigureAwait(false);
                        }
                        else
                        {
                            // Check that the message hasn't expired
                            string rawTtbr;
                            if (incomingMessage.Headers.TryGetValue(SqsTransportHeaders.TimeToBeReceived, out rawTtbr))
                            {
                                incomingMessage.Headers.Remove(SqsTransportHeaders.TimeToBeReceived);

                                var timeToBeReceived = TimeSpan.Parse(rawTtbr);

                                if (timeToBeReceived != TimeSpan.MaxValue)
                                {
                                    var sentDateTime = message.GetSentDateTime();
                                    var utcNow = DateTime.UtcNow;
                                    if (sentDateTime + timeToBeReceived <= utcNow)
                                    {
                                        // Message has expired.
                                        Logger.Warn($"Discarding expired message with Id {incomingMessage.MessageId}");
                                        messageExpired = true;
                                    }
                                }
                            }


                            if (!messageExpired)
                            {
                                var immediateProcessingAttempts = 0;

                                while (!errorHandled && !messageProcessedOk)
                                {
                                    try
                                    {
                                        await _maxConcurrencySempahore
                                            .WaitAsync(_cancellationTokenSource.Token)
                                            .ConfigureAwait(false);

                                        using (var messageContextCancellationTokenSource =
                                            new CancellationTokenSource())
                                        {
                                            var messageContext = new MessageContext(
                                                incomingMessage.MessageId,
                                                incomingMessage.Headers,
                                                incomingMessage.Body,
                                                transportTransaction,
                                                messageContextCancellationTokenSource,
                                                contextBag);

                                            await _onMessage(messageContext)
                                                .ConfigureAwait(false);

                                            messageProcessedOk = !messageContextCancellationTokenSource.IsCancellationRequested;
                                        }
                                    }
                                    catch (Exception ex)
                                        when (!(ex is OperationCanceledException && _cancellationTokenSource.IsCancellationRequested))
                                    {
                                        immediateProcessingAttempts++;
                                        var errorHandlerResult = ErrorHandleResult.RetryRequired;

                                        try
                                        {
                                            errorHandlerResult = await _onError(new ErrorContext(ex,
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
                                    finally
                                    {
                                        _maxConcurrencySempahore.Release();
                                    }
                                }
                            }

                            // Always delete the message from the queue.
                            // If processing failed, the _onError handler will have moved the message
                            // to a retry queue.
                            await DeleteMessage(SqsClient, S3Client, message, sqsTransportMessage, incomingMessage).ConfigureAwait(false);
                        }
                    });

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                    when (!(ex is OperationCanceledException && _cancellationTokenSource.IsCancellationRequested))
                {
                    Logger.Error("Exception thrown when consuming messages", ex);
                }
            } // while
        }

        async Task DeleteMessage(IAmazonSQS sqs,
            IAmazonS3 s3,
            Message message,
            SqsTransportMessage sqsTransportMessage,
            IncomingMessage incomingMessage)
        {
            await sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, _cancellationTokenSource.Token).ConfigureAwait(false);

            if (sqsTransportMessage != null)
            {
                if (!String.IsNullOrEmpty(sqsTransportMessage.S3BodyKey))
                {
                    try
                    {
                        await s3.DeleteObjectAsync(
                            new DeleteObjectRequest
                            {
                                BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                                Key = ConnectionConfiguration.S3KeyPrefix + incomingMessage.MessageId
                            },
                            _cancellationTokenSource.Token).ConfigureAwait(false);
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
                Logger.Warn("Couldn't delete message body from S3 because the SqsTransportMessage was null. Message body data will be aged out by the S3 lifecycle policy when the TTL expires.");
            }
        }

        CancellationTokenSource _cancellationTokenSource;
        List<Task> _consumerTasks;
        Func<ErrorContext, Task<ErrorHandleResult>> _onError;
        Func<MessageContext, Task> _onMessage;
        SemaphoreSlim _maxConcurrencySempahore;
        string _queueUrl;
        int _concurrencyLevel;

        static ILog Logger = LogManager.GetLogger(typeof(SqsMessagePump));
    }
}