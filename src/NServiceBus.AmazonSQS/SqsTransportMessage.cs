namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;
    using Transport;

    class SqsTransportMessage
    {
        /// <summary>
        /// Empty constructor. Required for deserialization.
        /// </summary>
        public SqsTransportMessage()
        {
        }

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
                Headers[SqsTransportHeaders.TimeToBeReceived] = discardConstraint.MaxTime.ToString();
            }

            Body = outgoingMessage.Body != null ? Convert.ToBase64String(outgoingMessage.Body) : "empty message";
        }

        public Dictionary<string, string> Headers { get; set; }

        public string Body { get; set; }

        public string S3BodyKey { get; set; }
    }
}