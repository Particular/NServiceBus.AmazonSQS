using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.CircuitBreakers;
using NServiceBus.Logging;
using NServiceBus.SQS;
using NServiceBus.Transports;
using NServiceBus.Unicast.Transport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQS
{
    internal class SqsDequeueStrategy : IDequeueMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public void Init(Address address, TransactionSettings transactionSettings, Func<TransportMessage, bool> tryProcessMessage, Action<TransportMessage, Exception> endProcessMessage)
        {
            using (var sqs = SqsClientFactory.CreateClient(ConnectionConfiguration))
            {
                var getQueueUrlRequest = new GetQueueUrlRequest(address.ToSqsQueueName());
                var getQueueUrlResponse = sqs.GetQueueUrl(getQueueUrlRequest);
                _queueUrl = getQueueUrlResponse.QueueUrl;
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
                using (var sqs = SqsClientFactory.CreateClient(ConnectionConfiguration))
                {
                    while (!_isStopping)
                    {
                        Exception exception = null;
                        var message = sqs.DequeueMessage(_queueUrl);

                        TransportMessage transportMessage = null;

                        try
                        {
                            var messageProcessedOk = false;

                            transportMessage = message.ToTransportMessage();

                            messageProcessedOk = _tryProcessMessage(transportMessage);

                            if (messageProcessedOk)
                            {
                                sqs.DeleteMessage(_queueUrl, message.ReceiptHandle);
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
