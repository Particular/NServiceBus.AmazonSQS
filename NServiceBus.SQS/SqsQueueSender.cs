using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using NServiceBus.SQS;
using NServiceBus.Unicast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQS
{
    class SqsQueueSender : ISendMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            using (var sqs = SqsClientFactory.CreateClient(ConnectionConfiguration))
            {
                sqs.SendTransportMessage(message, sendOptions);
            }
        }
    }
}
