using NServiceBus.Transport;
using System;
using System.Collections.Generic;

namespace NServiceBus.AmazonSQS
{
    internal class SqsTransportMessage
    {
        public SqsTransportMessage()
        {
        }

        public SqsTransportMessage(OutgoingMessage outgoingMessage)
        {
            Headers = outgoingMessage.Headers;

            Body = outgoingMessage.Body != null ? Convert.ToBase64String(outgoingMessage.Body) : "empty message";
        }

        public Dictionary<string, string> Headers { get; set; }

        public string Body { get; set; }

		public string S3BodyKey { get; set; }
    }
}
