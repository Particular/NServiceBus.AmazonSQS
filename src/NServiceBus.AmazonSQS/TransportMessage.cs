﻿namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DeliveryConstraints;
    using Performance.TimeToBeReceived;
    using Transport;

    class TransportMessage
    {
        // Empty constructor required for deserialization.
        public TransportMessage()
        {
        }

        public TransportMessage(OutgoingMessage outgoingMessage, List<DeliveryConstraint> deliveryConstraints)
        {
            Headers = outgoingMessage.Headers;

            Headers.TryGetValue(NServiceBus.Headers.MessageId, out var messageId);
            if (string.IsNullOrEmpty(messageId))
            {
                messageId = Guid.NewGuid().ToString();
                Headers[NServiceBus.Headers.MessageId] = messageId;
            }

            var discardConstraint = deliveryConstraints.OfType<DiscardIfNotReceivedBefore>().SingleOrDefault();
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
            get => Headers.ContainsKey(TransportHeaders.TimeToBeReceived) ? Headers[TransportHeaders.TimeToBeReceived] : TimeSpan.MaxValue.ToString();
            set
            {
                if (value != null)
                {
                    Headers[TransportHeaders.TimeToBeReceived] = value;
                }
            }
        }

        public string ReplyToAddress
        {
            get => Headers.ContainsKey(NServiceBus.Headers.ReplyToAddress) ? Headers[NServiceBus.Headers.ReplyToAddress] : null;
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