using Amazon.SQS.Model;
using NServiceBus.Transports.SQS;
using NServiceBus.Unicast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS.IntegrationTests
{
    class SqsTestContext
    {
        public static readonly string QueueName = "testQueue";

        public static readonly string MachineName = "testMachine";

        private Address _address;
        private SqsConnectionConfiguration _connectionConfig;
        private string _queueUrl;

        public SqsTestContext()
        {
            _address = new Address(QueueName, MachineName);
            _connectionConfig = new SqsConnectionConfiguration { Region = Amazon.RegionEndpoint.APSoutheast2 };

            using (var sqs = SqsClientFactory.CreateClient(_connectionConfig))
            {
                _queueUrl = sqs.CreateQueue(_address.ToSqsQueueName()).QueueUrl;

                // SQS only allows purging a queue once every 60 seconds or so. 
                // If you try to purge a queue twice in relatively quick succession,
                // PurgeQueueInProgressException will be thrown. 
                // This will happen if you are trying to run many integration test runs
                // in a short period of time.
                sqs.PurgeQueue(_queueUrl);
            }
        }

        public TransportMessage SendAndReceiveMessage(TransportMessage messageToSend, SendOptions sendOptions)
        {
            using (var sqs = SqsClientFactory.CreateClient(_connectionConfig))
            {
                sqs.SendTransportMessage( messageToSend, sendOptions );

                var message = sqs.DequeueMessage( _queueUrl );

                var result = message.ToTransportMessage();

                sqs.DeleteMessage(_queueUrl, message.ReceiptHandle);

                return result;
            }
        }
    }
}
