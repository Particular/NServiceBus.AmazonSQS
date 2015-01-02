using Amazon.SQS.Model;
using NServiceBus.Transports.SQS;
using NServiceBus.Unicast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.SQS.IntegrationTests
{
    class SqsTestContext : IDisposable
    {
        public static readonly string QueueName = "testQueue";

        public static readonly string MachineName = "testMachine";

        public SqsConnectionConfiguration ConnectionConfiguration { get; private set; }

        public string QueueUrl { get; private set; }

        public IObservable<TransportMessage> ReceivedMessages 
        {
            get { return _receivedMessages; }
        }

        public IObservable<Exception> ExceptionsThrownByReceiver 
        { 
            get { return _exceptionsThrownByReceiver; } 
        }

        private Address _address;
        private SqsDequeueStrategy _dequeue;
        private SqsQueueSender _sender;
        private Subject<TransportMessage> _receivedMessages;
        private Subject<Exception> _exceptionsThrownByReceiver;

        public SqsTestContext()
        {
            _address = new Address(QueueName, MachineName);
            ConnectionConfiguration = new SqsConnectionConfiguration { Region = Amazon.RegionEndpoint.APSoutheast2 };

            _receivedMessages = new Subject<TransportMessage>();
            _exceptionsThrownByReceiver = new Subject<Exception>();

            _sender = new SqsQueueSender();
            _sender.ConnectionConfiguration = ConnectionConfiguration;

            _dequeue = new SqsDequeueStrategy();
            _dequeue.ConnectionConfiguration = ConnectionConfiguration;
            _dequeue.Init(_address,
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

            _dequeue.Start(1);

            using (var sqs = SqsClientFactory.CreateClient(ConnectionConfiguration))
            {
                QueueUrl = sqs.CreateQueue(_address.ToSqsQueueName()).QueueUrl;

                // SQS only allows purging a queue once every 60 seconds or so. 
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown. 
                // This will happen if you are trying to run many integration test runs
                // in a short period of time.
                sqs.PurgeQueue(QueueUrl);
            }
        }

        public TransportMessage SendRawAndReceiveMessage(string rawMessageString)
        {
            return SendAndReceiveCore(() =>
                {
                    using (var c = SqsClientFactory.CreateClient(ConnectionConfiguration))
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
            return SendAndReceiveCore(() => _sender.Send(messageToSend, new Unicast.SendOptions(new Address(SqsTestContext.QueueName, SqsTestContext.MachineName))));
        }

        public void Dispose()
        {
            _dequeue.Stop();
        }
    }
}
