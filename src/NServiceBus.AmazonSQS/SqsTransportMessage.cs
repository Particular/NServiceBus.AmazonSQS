using NServiceBus.Transport;
using System;
using System.Collections.Generic;

namespace NServiceBus.AmazonSQS
{
    using System.Linq;
    using NServiceBus.DeliveryConstraints;
    using NServiceBus.Performance.TimeToBeReceived;

    internal class SqsTransportMessage
    {
        /// <summary>
        /// Empty constructor. Required for deserialization.
        /// </summary>
        public SqsTransportMessage() { }

        public SqsTransportMessage(OutgoingMessage outgoingMessage, List<DeliveryConstraint> deliveryConstrants)
        {
            Headers = outgoingMessage.Headers;

            string messageId;
            Headers.TryGetValue(NServiceBus.Headers.MessageId, out messageId);
            if (string.IsNullOrEmpty(messageId))
            {
                messageId = Guid.NewGuid().ToString();
                Headers[NServiceBus.Headers.MessageId] = messageId;
            }

            var discardConstraint = deliveryConstrants.OfType<DiscardIfNotReceivedBefore>().SingleOrDefault();
            if (discardConstraint != null)
            {
                TimeToBeReceived = discardConstraint.MaxTime.ToString();
            }

            Body = outgoingMessage.Body != null ? Convert.ToBase64String(outgoingMessage.Body) : "empty message";
        }

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        public string Body { get; set; }

        public string S3BodyKey { get; set; }

        public string TimeToBeReceived
        {
            get
            {
                return Headers.ContainsKey(SqsTransportHeaders.TimeToBeReceived) ? Headers[SqsTransportHeaders.TimeToBeReceived] : TimeSpan.MaxValue.ToString();
            }
            set
            {
                if (value != null)
                {
                    Headers[SqsTransportHeaders.TimeToBeReceived] = value;
                }
            }
        }

        public string ReplyToAddress
        {
            get
            {
                return Headers.ContainsKey(NServiceBus.Headers.ReplyToAddress) ? Headers[NServiceBus.Headers.ReplyToAddress] : null;
            }
            set
            {
                if (value != null)
                {
                    Headers[NServiceBus.Headers.ReplyToAddress] = value;
                }
            }
        }
    }
}
