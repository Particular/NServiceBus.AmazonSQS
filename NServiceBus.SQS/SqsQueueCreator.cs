using Amazon.SQS.Model;
using NServiceBus.SQS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQS
{
    class SqsQueueCreator : ICreateQueues
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public void CreateQueueIfNecessary(Address address, string account)
        {
            using (var sqs = SqsClientFactory.CreateClient(ConnectionConfiguration))
            {
                CreateQueueRequest sqsRequest = new CreateQueueRequest();
                sqsRequest.QueueName = address.ToSqsQueueName();
                sqs.CreateQueue(sqsRequest);
            }
        }
    }
}
