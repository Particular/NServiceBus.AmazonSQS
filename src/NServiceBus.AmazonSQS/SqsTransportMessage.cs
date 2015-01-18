using NServiceBus.Unicast;
using System;
using System.Collections.Generic;

namespace NServiceBus.AmazonSQS
{
    internal class SqsTransportMessage
    {
        public SqsTransportMessage()
        {
        }

        public SqsTransportMessage(TransportMessage transportMessage, SendOptions sendOptions)
        {
            Headers = transportMessage.Headers;

            Body = transportMessage.Body != null ? Convert.ToBase64String(transportMessage.Body) : "empty message";

            TimeToBeReceived = transportMessage.TimeToBeReceived;

			ReplyToAddress = sendOptions.ReplyToAddress;
        }

        public Dictionary<string, string> Headers { get; set; }

        public TimeSpan TimeToBeReceived { get; set; }

        public string Body { get; set; }

		public string S3BodyKey { get; set; }

		public Address ReplyToAddress { get; set; }
    }
}
