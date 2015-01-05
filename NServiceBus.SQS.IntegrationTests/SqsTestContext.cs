using Amazon.SQS.Model;
using NServiceBus.ObjectBuilder;
using NServiceBus.Pipeline;
using NServiceBus.Settings;
using NServiceBus.Transports.SQS;
using NServiceBus.Unicast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.ObjectBuilder.Common;

namespace NServiceBus.SQS.IntegrationTests
{
    class SqsTestContext : IDisposable
    {
        public static readonly string QueueName = "testQueue";

        public static readonly string MachineName = "testMachine";

        public SqsConnectionConfiguration ConnectionConfiguration { get; private set; }

		public IAwsClientFactory ClientFactory { get; private set; }

        public string QueueUrl { get; private set; }

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
        
        public SqsTestContext()
        {
            Address = new Address(QueueName, MachineName);
            ConnectionConfiguration = new SqsConnectionConfiguration 
			{ 
				Region = Amazon.RegionEndpoint.APSoutheast2,
				S3BucketForLargeMessages = "m1nf0sdevel0pment",
				S3KeyPrefix = "test"
			};

			ClientFactory = new AwsClientFactory();
			Creator = new SqsQueueCreator();
			Creator.ConnectionConfiguration = ConnectionConfiguration;
			Creator.ClientFactory = ClientFactory;
			Creator.CreateQueueIfNecessary(Address, "");

            _receivedMessages = new Subject<TransportMessage>();
            _exceptionsThrownByReceiver = new Subject<Exception>();

            Sender = new SqsQueueSender();
            Sender.ConnectionConfiguration = ConnectionConfiguration;
			Sender.ClientFactory = ClientFactory;

            var configure = new Configure(new SettingsHolder(), new FakeContainer(), new List<Action<IConfigureComponents>>(), new PipelineSettings(new BusConfiguration()));
            configure.Settings.Set("Transport.PurgeOnStartup", true);

            DequeueStrategy = new SqsDequeueStrategy(configure);
            DequeueStrategy.ConnectionConfiguration = ConnectionConfiguration;
			DequeueStrategy.ClientFactory = ClientFactory;
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

			using (var c = ClientFactory.CreateSqsClient(ConnectionConfiguration))
            {
                QueueUrl = c.GetQueueUrl(Address.ToSqsQueueName()).QueueUrl;
            }
        }

        public TransportMessage SendRawAndReceiveMessage(string rawMessageString)
        {
            return SendAndReceiveCore(() =>
                {
					using (var c = ClientFactory.CreateSqsClient(ConnectionConfiguration))
                    {
                        c.SendMessage(QueueUrl, rawMessageString);
                    }
                });
        }

        private TransportMessage SendAndReceiveCore(Action doSend)
        {
            // Definitely not thread safe; none of the integration tests that use
            // a single SqsTestContext instance can run in parallel. 

            TransportMessage lastReceivedMessage = null;
            Exception lastThrownException = null;

            int retryCount = 0;

            using (var receiveSubscription = ReceivedMessages.Subscribe(m => lastReceivedMessage = m))
            using (var exceptionSubscription = ExceptionsThrownByReceiver.Subscribe(e => lastThrownException = e))
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
            return SendAndReceiveCore(() => Sender.Send(messageToSend, new Unicast.SendOptions(new Address(SqsTestContext.QueueName, SqsTestContext.MachineName))));
        }

        public void Dispose()
        {
            DequeueStrategy.Stop();
        }
    }

    class FakeContainer : IContainer
    {
        public void Dispose()
        {
        }

        public object Build(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public IContainer BuildChildContainer()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> BuildAll(Type typeToBuild)
        {
            throw new NotImplementedException();
        }

        public void Configure(Type component, DependencyLifecycle dependencyLifecycle)
        {

        }

        public void Configure<T>(Func<T> component, DependencyLifecycle dependencyLifecycle)
        {

        }

        public void ConfigureProperty(Type component, string property, object value)
        {

        }

        public void RegisterSingleton(Type lookupType, object instance)
        {

        }

        public bool HasComponent(Type componentType)
        {
            throw new NotImplementedException();
        }

        public void Release(object instance)
        {

        }
    }
}
