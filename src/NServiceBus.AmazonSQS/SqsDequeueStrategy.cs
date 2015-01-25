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
    using Unicast.Transport;

    internal class SqsDequeueStrategy : IDequeueMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public SqsDequeueStrategy(Configure config)
        {
			if (config != null)
				_purgeOnStartup = config.PurgeOnStartup();
			else
				_purgeOnStartup = false;
        }

        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            var getQueueUrlRequest = new GetQueueUrlRequest(address.ToSqsQueueName(ConnectionConfiguration));
            GetQueueUrlResponse getQueueUrlResponse;
            try
            {
                getQueueUrlResponse = SqsClient.GetQueueUrl(getQueueUrlRequest);
            }
            catch (Exception ex)
            {
                Logger.Error("Exception thrown from GetQueueUrl.", ex);
                throw;
            }
            _queueUrl = getQueueUrlResponse.QueueUrl;

			if (_purgeOnStartup)
            {
                // SQS only allows purging a queue once every 60 seconds or so. 
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown. 
                // This will happen if you are trying to start an endpoint twice or more
                // in that time. 
                try
                {
                    SqsClient.PurgeQueue(_queueUrl);
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

            _tryProcessMessage = tryProcessMessage;
            _endProcessMessage = endProcessMessage;

			if (transactionSettings != null)
				_isTransactional = transactionSettings.IsTransactional;
			else
				_isTransactional = true;
        }

        public void Start(int maximumConcurrencyLevel)
        {
			_cancellationTokenSource = new CancellationTokenSource();
            _concurrencyLevel = maximumConcurrencyLevel;

            _tracksRunningThreads = new SemaphoreSlim(_concurrencyLevel);

            for (var i = 0; i < maximumConcurrencyLevel; i++)
            {
                StartConsumer();
            }
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
			if ( _cancellationTokenSource != null )
				_cancellationTokenSource.Cancel();

            DrainStopSemaphore();
        }

        void DrainStopSemaphore()
        {
	        if (_tracksRunningThreads != null)
	        {
				for (var index = 0; index < _concurrencyLevel; index++)
				{
					_tracksRunningThreads.Wait();
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

        void ConsumeMessages()
        {
            _tracksRunningThreads.Wait(TimeSpan.FromSeconds(1));

            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Exception exception = null;
						
					var receiveTask = SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
						{
							MaxNumberOfMessages = ConnectionConfiguration.MaxReceiveMessageBatchSize,
							QueueUrl = _queueUrl,
							WaitTimeSeconds = 20,
							AttributeNames = new List<String> { "SentTimestamp" }
						},
						_cancellationTokenSource.Token);

					receiveTask.Wait(_cancellationTokenSource.Token);

                    foreach (var message in receiveTask.Result.Messages)
                    {
                        TransportMessage transportMessage = null;
                        SqsTransportMessage sqsTransportMessage = null;

                        var messageProcessedOk = false;
                        var messageExpired = false;

                        try
                        {
                            sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

                            transportMessage = sqsTransportMessage.ToTransportMessage(S3Client, ConnectionConfiguration);

                            // Check that the message hasn't expired
                            if (transportMessage.TimeToBeReceived != TimeSpan.MaxValue)
                            {
                                var sentDateTime = message.GetSentDateTime();
                                if (sentDateTime + transportMessage.TimeToBeReceived <= DateTime.UtcNow)
                                {
                                    // Message has expired. 
                                    Logger.Warn(String.Format("Discarding expired message with Id {0}", transportMessage.Id));
                                    messageExpired = true;
                                }
                            }

                            if (!messageExpired)
                            {
                                messageProcessedOk = _tryProcessMessage(transportMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }
                        finally
                        {
                            var deleteMessage = !_isTransactional || (_isTransactional && messageProcessedOk);

                            if (deleteMessage)
                            {
                                DeleteMessage(SqsClient, S3Client, message, sqsTransportMessage, transportMessage);
                            }
                            else
                            {
                                SqsClient.ChangeMessageVisibility(_queueUrl, message.ReceiptHandle, 0);
                            }

                            _endProcessMessage(transportMessage, exception);
                        }
                    }
                }
            }
            finally
            {
                _tracksRunningThreads.Release();
            }
        }

		private void DeleteMessage(IAmazonSQS sqs, 
			IAmazonS3 s3,
			Message message, 
			SqsTransportMessage sqsTransportMessage, 
			TransportMessage transportMessage)
		{
			sqs.DeleteMessage(_queueUrl, message.ReceiptHandle);

			if (!String.IsNullOrEmpty(sqsTransportMessage.S3BodyKey))
			{
				// Delete the S3 body asynchronously. 
				// We don't really care too much if this call succeeds or fails - if it fails, 
				// the S3 bucket lifecycle configuration will eventually delete the message anyway.
				// So, we can get better performance by not waiting around for this call to finish.
				var s3DeleteTask = s3.DeleteObjectAsync(
					new DeleteObjectRequest
					{
						BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
						Key = ConnectionConfiguration.S3KeyPrefix + transportMessage.Id
					});

				s3DeleteTask.ContinueWith(t =>
				{
					if (t.Exception != null)
					{
						// If deleting the message body from S3 fails, we don't 
						// want the exception to make its way through to the _endProcessMessage below,
						// as the message has been successfully processed and deleted from the SQS queue
						// and effectively doesn't exist anymore. 
						// It doesn't really matter, as S3 is configured to delete message body data
						// automatically after a certain period of time.
						Logger.Warn("Couldn't delete message body from S3. Message body data will be aged out at a later time.", t.Exception);
					}
				});
			}
		}

        static ILog Logger = LogManager.GetLogger(typeof(SqsDequeueStrategy));

		CancellationTokenSource _cancellationTokenSource;
        SemaphoreSlim _tracksRunningThreads;
        Action<TransportMessage, Exception> _endProcessMessage;
        Func<TransportMessage, bool> _tryProcessMessage;
        string _queueUrl;
        int _concurrencyLevel;
		bool _purgeOnStartup;
		bool _isTransactional;
    }
}
