namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using Transport;

    class TransportMessage
    {
        //MessageBody of Amazon.SQS SendRequest must have a minimum length of 1: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_SendMessage.html
        public const string EmptyMessage = "empty message";

        // Empty constructor required for deserialization.
        public TransportMessage()
        {
        }

        public TransportMessage(OutgoingMessage outgoingMessage, DispatchProperties properties)
        {
            Headers = outgoingMessage.Headers;

            Headers.TryGetValue(NServiceBus.Headers.MessageId, out var messageId);
            if (string.IsNullOrEmpty(messageId))
            {
                messageId = Guid.NewGuid().ToString();
                Headers[NServiceBus.Headers.MessageId] = messageId;
            }

            if (properties.DiscardIfNotReceivedBefore != null)
            {
                TimeToBeReceived = properties.DiscardIfNotReceivedBefore.MaxTime.ToString();
            }

            if (outgoingMessage.Body.Length != 0)
            {
                Body = Convert.ToBase64String(outgoingMessage.Body.Span);
            }
            else
            {
                Body = EmptyMessage;
            }
        }

        [System.Text.Json.Serialization.JsonRequired]
        public Dictionary<string, string> Headers { get; set; }

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

        public Address? ReplyToAddress
        {
            get => Headers.ContainsKey(NServiceBus.Headers.ReplyToAddress) ? new Address { Queue = Headers[NServiceBus.Headers.ReplyToAddress] } : null;
            set
            {
                if (!string.IsNullOrWhiteSpace(value?.Queue))
                {
                    Headers[NServiceBus.Headers.ReplyToAddress] = value.Value.Queue;
                }
            }
        }

        public struct Address
        {
            public string Queue { get; set; }
            public string Machine { get; set; }
        }
    }
}