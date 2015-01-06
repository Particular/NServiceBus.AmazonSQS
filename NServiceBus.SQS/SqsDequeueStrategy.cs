namespace NServiceBus.Transports.SQS
{
	using Amazon.Runtime;
	using Amazon.SQS;
	using Amazon.SQS.Model;
	using Newtonsoft.Json;
	using NServiceBus.Logging;
	using NServiceBus.SQS;
	using NServiceBus.Transports;
	using NServiceBus.Unicast.Transport;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

    internal class SqsDequeueStrategy : IDequeueMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

		public IAwsClientFactory ClientFactory { get; set; }

		public bool PurgeOnStartup { get; set; }

        public SqsDequeueStrategy()
        {
        }

        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
			using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
            {
                var getQueueUrlRequest = new GetQueueUrlRequest(address.ToSqsQueueName());
                var getQueueUrlResponse = sqs.GetQueueUrl(getQueueUrlRequest);
                _queueUrl = getQueueUrlResponse.QueueUrl;

                if (PurgeOnStartup)
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
        }

        public void Start(int maximumConcurrencyLevel)
        {
            _isStopping = false;
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
            _isStopping = true;

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
                .StartNew(ConsumeMessages, TaskCreationOptions.LongRunning)
                .ContinueWith(t =>
                {
                    if (!_isStopping)
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
                    while (!_isStopping)
                    {
                        Exception exception = null;
                        var message = sqs.DequeueMessage(_queueUrl);

						if (message == null)
							continue;

                        TransportMessage transportMessage = null;

                        try
                        {
                            var messageProcessedOk = false;

							var sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

                            transportMessage = sqsTransportMessage.ToTransportMessage(s3, ConnectionConfiguration);

                            messageProcessedOk = _tryProcessMessage(transportMessage);

                            if (messageProcessedOk)
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
										
									s3DeleteTask.Start();
								}
                            }
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }
                        finally
                        {
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

        static ILog Logger = LogManager.GetLogger(typeof(SqsDequeueStrategy));

        SemaphoreSlim _tracksRunningThreads;
        volatile bool _isStopping;
        Action<TransportMessage, Exception> _endProcessMessage;
        Func<TransportMessage, bool> _tryProcessMessage;
        string _queueUrl;
        int _concurrencyLevel;
    }
}
