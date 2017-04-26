namespace NServiceBus.AmazonSQS.IntegrationTests
{
    using Amazon.SQS.Model;
    using System;
    using System.Reactive.Subjects;
    using System.Threading;
    using System.Configuration;
    using Amazon.S3;
    using Amazon.SQS;
    using Transports.SQS;
    using Unicast;
    using System.Diagnostics;

    internal class SqsTestContext : IDisposable
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; private set; }

		public IAmazonSQS SqsClient { get; set; }

        public IAmazonS3 S3Client { get; set; }

		public SqsQueueUrlCache QueueUrlCache { get; private set; }

        public IObservable<TransportMessage> ReceivedMessages 
        {
            get { return _receivedMessages; }
        }

        public IObservable<Exception> ExceptionsThrownByReceiver 
        { 
            get { return _exceptionsThrownByReceiver; } 
        }

        public SqsDequeueStrategy DequeueStrategy { get; private set; }

        public SqsQueueSender Sender { get; private set; }

		public SqsQueueCreator Creator { get; private set; }

        public Address Address { get; private set; }

        private Subject<TransportMessage> _receivedMessages;
        private Subject<Exception> _exceptionsThrownByReceiver;
        
        public SqsTestContext(object fixture)
        {
            Address = new Address(fixture.GetType().Name, Environment.MachineName);
			ConnectionConfiguration = 
				SqsConnectionStringParser.Parse(ConfigurationManager.AppSettings["TestConnectionString"]);

            S3Client = AwsClientFactory.CreateS3Client(ConnectionConfiguration);
            SqsClient = AwsClientFactory.CreateSqsClient(ConnectionConfiguration);

			Creator = new SqsQueueCreator
			{
				ConnectionConfiguration = ConnectionConfiguration,
				SqsClient = SqsClient,
                S3Client = S3Client
			};
	        
            _receivedMessages = new Subject<TransportMessage>();
            _exceptionsThrownByReceiver = new Subject<Exception>();

			QueueUrlCache = new SqsQueueUrlCache
			{
                SqsClient = SqsClient,
				ConnectionConfiguration = ConnectionConfiguration
			};


            Sender = new SqsQueueSender
            {
	            ConnectionConfiguration = ConnectionConfiguration,
	            SqsClient = SqsClient,
                S3Client = S3Client,
	            QueueUrlCache = QueueUrlCache,
				QueueCreator = Creator
            };

	        DequeueStrategy = new SqsDequeueStrategy(null)
	        {
		        ConnectionConfiguration = ConnectionConfiguration,
                SqsClient = SqsClient,
                S3Client = S3Client
            };
	        
        }

	    public void CreateQueue()
	    {
			Creator.CreateQueueIfNecessary(Address, "");
	    }

        public void PurgeQueue()
        {   
            try
            {
                SqsClient.PurgeQueue(QueueUrlCache.GetQueueUrl(Address));
            }
            catch (PurgeQueueInProgressException)
            {

            }

            int approxNumberOfMessages = 0;
            do
            {
                Thread.Sleep(2000);

                // The purge operation above may not have succeeded - there is an 
                // annoying restriction that we can only do one purge every 60 seconds.
                // In that case, try to manually delete everything in the queue.
                var messages = SqsClient.ReceiveMessage(new ReceiveMessageRequest
                {
                    QueueUrl = QueueUrlCache.GetQueueUrl(Address),
                    MaxNumberOfMessages = 10
                });

                approxNumberOfMessages = messages.Messages.Count;

                foreach (var m in messages.Messages)
                {
                    SqsClient.DeleteMessage(QueueUrlCache.GetQueueUrl(Address), m.ReceiptHandle);
                }

            } while (approxNumberOfMessages != 0);
        }

		public void InitAndStartDequeueing()
		{
			DequeueStrategy.Init(Address,
				null,
				m =>
				{
					_receivedMessages.OnNext(m);
					return true;
				},
				(m, e) =>
				{
					if (e != null)
						_exceptionsThrownByReceiver.OnNext(e);
				});
			DequeueStrategy.Start(1);	
		}

        public TransportMessage SendRawAndReceiveMessage(string rawMessageString)
        {
            return SendAndReceiveCore(() =>
                {
                    SqsClient.SendMessage(QueueUrlCache.GetQueueUrl(Address), rawMessageString);
                });
        }

        private TransportMessage SendAndReceiveCore(Action doSend)
        {
            // Definitely not thread safe; none of the integration tests that use
            // a single SqsTestContext instance can run in parallel. 

            TransportMessage lastReceivedMessage = null;
            Exception lastThrownException = null;

            var retryCount = 0;
            const int maxRetryCount = 100;

            using (ReceivedMessages.Subscribe(m => lastReceivedMessage = m))
            using (ExceptionsThrownByReceiver.Subscribe(e => lastThrownException = e))
            {
                doSend();

                while (lastReceivedMessage == null && lastThrownException == null && retryCount < maxRetryCount)
                {
                    retryCount++;
                    Thread.Sleep(50);
                }
            }

            if (retryCount >= maxRetryCount)
                throw new TimeoutException("Receiving a message timed out.");

            if (lastThrownException == null)
                return lastReceivedMessage;
            else
            {
                Trace.WriteLine($"Exception from {nameof(SendAndReceiveCore)}: {lastThrownException}"); 
                throw lastThrownException;
            }
                
        }

        public TransportMessage SendAndReceiveMessage(TransportMessage messageToSend)
        {
			return SendAndReceiveCore(() => Sender.Send(messageToSend, new SendOptions(Address)));
        }

        public void Dispose()
        {
            DequeueStrategy.Stop();

            if (S3Client != null)
                S3Client.Dispose();
            if (SqsClient != null)
                SqsClient.Dispose();
        }

		
    }
}
