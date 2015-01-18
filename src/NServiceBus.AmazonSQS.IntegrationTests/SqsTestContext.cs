namespace NServiceBus.AmazonSQS.IntegrationTests
{
	using Amazon.SQS.Model;
	using System;
	using System.Reactive.Subjects;
	using System.Threading;
	using System.Configuration;
	using Transports.SQS;
	using Unicast;
	
	internal class SqsTestContext : IDisposable
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; private set; }

		public IAwsClientFactory ClientFactory { get; private set; }

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

			ClientFactory = new AwsClientFactory();
			Creator = new SqsQueueCreator
			{
				ConnectionConfiguration = ConnectionConfiguration,
				ClientFactory = ClientFactory
			};
	        
            _receivedMessages = new Subject<TransportMessage>();
            _exceptionsThrownByReceiver = new Subject<Exception>();

			QueueUrlCache = new SqsQueueUrlCache
			{
				ClientFactory = ClientFactory,
				ConnectionConfiguration = ConnectionConfiguration
			};


            Sender = new SqsQueueSender
            {
	            ConnectionConfiguration = ConnectionConfiguration,
	            ClientFactory = ClientFactory,
	            QueueUrlCache = QueueUrlCache,
				QueueCreator = Creator
            };

	        DequeueStrategy = new SqsDequeueStrategy(null)
	        {
		        ConnectionConfiguration = ConnectionConfiguration,
		        ClientFactory = ClientFactory
	        };
	        
        }

	    public void CreateQueue()
	    {
			Creator.CreateQueueIfNecessary(Address, "");

			using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
			{
				try
				{
					sqs.PurgeQueue(QueueUrlCache.GetQueueUrl(Address));
				}
				catch (PurgeQueueInProgressException)
				{

				}
			}
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
					using (var c = ClientFactory.CreateSqsClient(ConnectionConfiguration))
                    {
                        c.SendMessage(QueueUrlCache.GetQueueUrl(Address), rawMessageString);
                    }
                });
        }

        private TransportMessage SendAndReceiveCore(Action doSend)
        {
            // Definitely not thread safe; none of the integration tests that use
            // a single SqsTestContext instance can run in parallel. 

            TransportMessage lastReceivedMessage = null;
            Exception lastThrownException = null;

            var retryCount = 0;

            using (ReceivedMessages.Subscribe(m => lastReceivedMessage = m))
            using (ExceptionsThrownByReceiver.Subscribe(e => lastThrownException = e))
            {
                doSend();

                while (lastReceivedMessage == null && lastThrownException == null && retryCount < 100)
                {
                    retryCount++;
                    Thread.Sleep(50);
                }
            }

            if (retryCount >= 100)
                throw new TimeoutException("Receiving a message timed out.");

            if (lastThrownException == null)
                return lastReceivedMessage;
            else
                throw lastThrownException;
        }

        public TransportMessage SendAndReceiveMessage(TransportMessage messageToSend)
        {
			return SendAndReceiveCore(() => Sender.Send(messageToSend, new SendOptions(Address)));
        }

        public void Dispose()
        {
            DequeueStrategy.Stop();
        }

		
    }
}
