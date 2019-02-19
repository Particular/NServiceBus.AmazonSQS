namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    static class PreparedMessageExtensions
    {
        public static SendMessageRequest ToRequest(this PreparedMessage message)
        {
            return new SendMessageRequest(message.QueueUrl, message.Body)
            {
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                MessageAttributes = message.MessageAttributes,
                DelaySeconds = Convert.ToInt32(message.DelaySeconds)
            };
        }

        public static SendMessageBatchRequestEntry ToBatchEntry(this PreparedMessage message)
        {
            return new SendMessageBatchRequestEntry(message.MessageId, message.Body)
            {
                MessageAttributes = message.MessageAttributes,
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                DelaySeconds = Convert.ToInt32(message.DelaySeconds)
            };
        }

        public static SendMessageBatchRequest ToBatchRequest(this PreparedMessage message, IEnumerable<SendMessageBatchRequestEntry> batchEntries)
        {
            return new SendMessageBatchRequest(message.QueueUrl, new List<SendMessageBatchRequestEntry>(batchEntries));
        }
    }
}