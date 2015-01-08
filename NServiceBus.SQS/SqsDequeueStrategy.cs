namespace NServiceBus.Transports.SQS
{
	using Amazon.Runtime;
	using Amazon.S3;
	using Amazon.SQS;
	using Amazon.SQS.Model;
	using Newtonsoft.Json;
	using NServiceBus.Logging;
	using NServiceBus.SQS;
	using NServiceBus.Transports;
	using NServiceBus.Unicast.Transport;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

    internal class SqsDequeueStrategy : IDequeueMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

		public IAwsClientFactory ClientFactory { get; set; }

        public SqsDequeueStrategy(Configure config)
        {
			if (config != null)
				_purgeOnStartup = config.PurgeOnStartup();
			else
				_purgeOnStartup = false;
        }

        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
			using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
            {
                var getQueueUrlRequest = new GetQueueUrlRequest(address.ToSqsQueueName());
                var getQueueUrlResponse = sqs.GetQueueUrl(getQueueUrlRequest);
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
                        sqs.PurgeQueue(_queueUrl);
                    }
                    catch (PurgeQueueInProgressException ex)
                    {
                        Logger.Warn("Multiple queue purges within 60 seconds are not permitted by SQS.", ex);
                    }
                }
            }

            _tryProcessMessage = tryProcessMessage;
            _endProcessMessage = endProcessMessage;

			_isTransactional = transactionSettings.IsTransactional;
        }

        public void Start(int maximumConcurrencyLevel)
        {
			_cancellationTokenSource = new CancellationTokenSource();
            _concurrencyLevel = maximumConcurrencyLevel;

            _tracksRunningThreads = new SemaphoreSlim(_concurrencyLevel);

            for (var i = 0; i < maximumConcurrencyLevel; i++)
            {
                StartConsumer(_queueUrl);
            }
        }

        /// <summary>
        ///     Stops the dequeuing of messages.
        /// </summary>
        public void Stop()
        {
			_cancellationTokenSource.Cancel();

            DrainStopSemaphore();
        }

        void DrainStopSemaphore()
        {
            for (var index = 0; index < _concurrencyLevel; index++)
            {
                _tracksRunningThreads.Wait();
            }

            _tracksRunningThreads.Release(_concurrencyLevel);

            _tracksRunningThreads.Dispose();
        }

        void StartConsumer(string queue)
        {
            Task.Factory
                .StartNew(ConsumeMessages, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .ContinueWith(t =>
                {
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        StartConsumer(queue);
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        void ConsumeMessages()
        {
            _tracksRunningThreads.Wait(TimeSpan.FromSeconds(1));

            try
            {
				using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
				using (var s3 = ClientFactory.CreateS3Client(ConnectionConfiguration))
                {
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        Exception exception = null;
						
						var receiveTask = sqs.ReceiveMessageAsync(new ReceiveMessageRequest
							{
								MaxNumberOfMessages = 1,
								QueueUrl = _queueUrl,
								WaitTimeSeconds = 20,
								AttributeNames = new List<String> { "SentTimestamp" }
							},
							_cancellationTokenSource.Token);

						receiveTask.Wait(_cancellationTokenSource.Token);

						var message = receiveTask.Result.Messages.FirstOrDefault();

						if (message == null)
							continue;

                        TransportMessage transportMessage = null;
						SqsTransportMessage sqsTransportMessage = null;

						var messageProcessedOk = false;
						var messageExpired = false;

                        try
                        {
							sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

                            transportMessage = sqsTransportMessage.ToTransportMessage(s3, ConnectionConfiguration);

							// Check that the message hasn't expired
							if (transportMessage.TimeToBeReceived != TimeSpan.MaxValue)
							{
								DateTime sentDateTime = message.GetSentDateTime();
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
							bool deleteMessage = !_isTransactional || (_isTransactional && messageProcessedOk);

							if (deleteMessage)
							{
								DeleteMessage(sqs, s3, message, sqsTransportMessage, transportMessage);
							}
							else
							{
								sqs.ChangeMessageVisibility(_queueUrl, message.ReceiptHandle, 0);
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
					new Amazon.S3.Model.DeleteObjectRequest
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
