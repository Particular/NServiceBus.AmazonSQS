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

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            var sqsQueueName = settings.InputQueue;
            var getQueueUrlRequest = new GetQueueUrlRequest(sqsQueueName);
            GetQueueUrlResponse getQueueUrlResponse;
            try
            {
                getQueueUrlResponse = await SqsClient.GetQueueUrlAsync(getQueueUrlRequest);
            }
            catch (Exception ex)
            {
                Logger.Error("Exception thrown from GetQueueUrl.", ex);
                throw;
            }
            _queueUrl = getQueueUrlResponse.QueueUrl;

			if (settings.PurgeOnStartup)
            {
                // SQS only allows purging a queue once every 60 seconds or so. 
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown. 
                // This will happen if you are trying to start an endpoint twice or more
                // in that time. 
                try
                {
                    await SqsClient.PurgeQueueAsync(_queueUrl);
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

            _tracksRunningThreads = new SemaphoreSlim(_concurrencyLevel);

            for (var i = 0; i < _concurrencyLevel; i++)
            {
                StartConsumer();
            }
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public Task Stop()
        {
			if ( _cancellationTokenSource != null )
				_cancellationTokenSource.Cancel();

            return DrainStopSemaphore();
        }

        async Task DrainStopSemaphore()
        {
	        if (_tracksRunningThreads != null)
	        {
				for (var index = 0; index < _concurrencyLevel; index++)
				{
					await _tracksRunningThreads.WaitAsync();
				}

				_tracksRunningThreads.Release(_concurrencyLevel);

				_tracksRunningThreads.Dispose();

		        _tracksRunningThreads = null;
	        }
        }

        void StartConsumer()
        {
            Task.Factory
                .StartNew(ConsumeMessages, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        StartConsumer();
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        async Task ConsumeMessages()
        {
            _tracksRunningThreads.Wait(TimeSpan.FromSeconds(1));

            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Exception exception = null;
						
					var receiveResult = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
						{
							MaxNumberOfMessages = ConnectionConfiguration.MaxReceiveMessageBatchSize,
							QueueUrl = _queueUrl,
							WaitTimeSeconds = 20,
							AttributeNames = new List<String> { "SentTimestamp" }
						},
						_cancellationTokenSource.Token);

                    foreach (var message in receiveResult.Messages)
                    {
                        IncomingMessage incomingMessage = null;
                        SqsTransportMessage sqsTransportMessage = null;
                        TransportTransaction transportTransaction = new TransportTransaction();
                        ContextBag contextBag = new ContextBag();

                        var messageProcessedOk = false;
                        var messageExpired = false;

                        try
                        {
                            sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

                            incomingMessage = await sqsTransportMessage.ToIncomingMessage(S3Client, ConnectionConfiguration);

                            // Check that the message hasn't expired
                            var timeToBeReceived = incomingMessage.GetTimeToBeReceived();
                            if (timeToBeReceived != TimeSpan.MaxValue)
                            {
                                var sentDateTime = message.GetSentDateTime();
                                if (sentDateTime + timeToBeReceived <= DateTime.UtcNow)
                                {
                                    // Message has expired. 
                                    Logger.Warn(String.Format("Discarding expired message with Id {0}", incomingMessage.MessageId));
                                    messageExpired = true;
                                }
                            }

                            if (!messageExpired)
                            {
                                MessageContext messageContext = new MessageContext(
                                    incomingMessage.MessageId,
                                    incomingMessage.Headers,
                                    incomingMessage.Body,
                                    transportTransaction,
                                    _cancellationTokenSource,
                                    contextBag);

                                await _onMessage(messageContext);
                            }
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }
                        finally
                        {
                            var deleteMessage = !_isTransactional || (_isTransactional && messageProcessedOk);

                            // If incomingMessage is null, the sqsTransportMessage could not be deserialized
                            // or otherwise converted to a transport message. This message can be considered
                            // a poison message and should be deleted.
                            if (incomingMessage == null)
                            {
                                Logger.Warn($"Deleting poison message with SQS Message Id {message.MessageId}");
                                deleteMessage = true;
                            }

                            if (deleteMessage)
                            {
                                await DeleteMessage(SqsClient, S3Client, message, sqsTransportMessage, incomingMessage);
                            }
                            else
                            {
                                await SqsClient.ChangeMessageVisibilityAsync(_queueUrl, message.ReceiptHandle, 0);
                            }

                            if (exception != null)
                            {
                                await _onError(new ErrorContext(exception,
                                    incomingMessage.Headers,
                                    incomingMessage.MessageId,
                                    incomingMessage.Body,
                                    transportTransaction,
                                    1));
                            }
                        }
                    }
                }
            }
            finally
            {
                _tracksRunningThreads.Release();
            }
        }

		async Task DeleteMessage(IAmazonSQS sqs, 
			IAmazonS3 s3,
			Message message, 
			SqsTransportMessage sqsTransportMessage, 
			IncomingMessage incomingMessage)
		{
			await sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);

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
                            });
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
        SemaphoreSlim _tracksRunningThreads;
        Func<ErrorContext, Task<ErrorHandleResult>> _onError;
        Func<MessageContext, Task> _onMessage;
        string _queueUrl;
        int _concurrencyLevel;
		bool _isTransactional;
    }
}
