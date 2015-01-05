using Amazon.SQS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS
{
    internal class SqsTransportMessage
    {
        public SqsTransportMessage()
        {
        }

        public SqsTransportMessage(TransportMessage transportMessage)
        {
            Headers = transportMessage.Headers;

            Body = transportMessage.Body != null ? Convert.ToBase64String(transportMessage.Body) : "empty message";

            TimeToBeReceived = transportMessage.TimeToBeReceived;
        }

        public Dictionary<string, string> Headers { get; set; }

        public TimeSpan TimeToBeReceived { get; set; }

        public string Body { get; set; }

		public string S3BodyKey { get; set; }
    }
}
