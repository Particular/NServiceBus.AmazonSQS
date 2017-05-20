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
    using Newtonsoft.Json;
    using Logging;
    using NServiceBus.AmazonSQS;
    using Transport;
    using Extensibility;

    internal class SqsMessagePump : IPushMessages
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

            _isTransactional = settings.RequiredTransactionMode != TransportTransactionMode.None;
        }

        public void Start(PushRuntimeSettings limitations)
        {
			_cancellationTokenSource = new CancellationTokenSource();
            _concurrencyLevel = limitations.MaxConcurrency;

            _consumerTasks = new List<Task>();

            for (var i = 0; i < _concurrencyLevel; i++)
            {
                _consumerTasks.Add(ConsumeMessages());
            }
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public Task Stop()
        {
            _cancellationTokenSource?.Cancel();

            return Task.WhenAll(_consumerTasks.ToArray());
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
                        AttributeNames = new List<String> { "SentTimestamp" }
                    },
                    _cancellationTokenSource.Token).ConfigureAwait(false);

                    foreach (var message in receiveResult.Messages)
                    {
                        IncomingMessage incomingMessage = null;
                        SqsTransportMessage sqsTransportMessage = null;
                        TransportTransaction transportTransaction = new TransportTransaction();
                        ContextBag contextBag = new ContextBag();

                        var messageProcessedOk = false;
                        var messageExpired = false;
                        var isPoisonMessage = false;
                        var errorHandled = false;

                        try
                        {
                            sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

                            incomingMessage = await sqsTransportMessage.ToIncomingMessage(S3Client, ConnectionConfiguration).ConfigureAwait(false);
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
                            await DeleteMessage(SqsClient, S3Client, message, sqsTransportMessage, incomingMessage).ConfigureAwait(false);
                        }
                        else
                        {
                            // Check that the message hasn't expired
                            var timeToBeReceived = incomingMessage.GetTimeToBeReceived();
                            if (timeToBeReceived.HasValue && timeToBeReceived.Value != TimeSpan.MaxValue)
                            {
                                var sentDateTime = message.GetSentDateTime();
                                if (sentDateTime + timeToBeReceived.Value <= DateTime.UtcNow)
                                {
                                    // Message has expired.
                                    Logger.Warn($"Discarding expired message with Id {incomingMessage.MessageId}");
                                    messageExpired = true;
                                }
                            }

                            if (!messageExpired)
                            {
                                int immediateProcessingAttempts = 0;

                                while (!errorHandled && !messageProcessedOk)
                                {
                                    try
                                    {
                                        MessageContext messageContext = new MessageContext(
                                        incomingMessage.MessageId,
                                        incomingMessage.Headers,
                                        incomingMessage.Body,
                                        transportTransaction,
                                        _cancellationTokenSource,
                                        contextBag);

                                        await _onMessage(messageContext)
                                            .ConfigureAwait(false);

                                        messageProcessedOk = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        immediateProcessingAttempts++;
                                        var errorHandlerResult = await _onError(new ErrorContext(ex,
                                            incomingMessage.Headers,
                                            incomingMessage.MessageId,
                                            incomingMessage.Body,
                                            transportTransaction,
                                            immediateProcessingAttempts)).ConfigureAwait(false);
                                        errorHandled = (errorHandlerResult == ErrorHandleResult.Handled);
                                    }
                                }
                            }

                            var deleteMessage = !_isTransactional || (_isTransactional && (messageProcessedOk || messageExpired || errorHandled));

                            if (deleteMessage)
                            {
                                await DeleteMessage(SqsClient, S3Client, message, sqsTransportMessage, incomingMessage).ConfigureAwait(false);
                            }
                            else
                            {
                                await SqsClient.ChangeMessageVisibilityAsync(_queueUrl,
                                    message.ReceiptHandle,
                                    0,
                                    _cancellationTokenSource.Token).ConfigureAwait(false);
                            }
                        }
                    } //foreach
                } // try
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming messages", ex);
                }
            }// while
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

        static ILog Logger = LogManager.GetLogger(typeof(SqsMessagePump));

		CancellationTokenSource _cancellationTokenSource;
        List<Task> _consumerTasks;
        Func<ErrorContext, Task<ErrorHandleResult>> _onError;
        Func<MessageContext, Task> _onMessage;
        string _queueUrl;
        int _concurrencyLevel;
		bool _isTransactional;
    }
}
