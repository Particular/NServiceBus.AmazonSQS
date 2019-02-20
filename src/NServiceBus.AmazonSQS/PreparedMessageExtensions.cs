namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        static SendMessageBatchRequestEntry ToBatchEntry(this PreparedMessage message)
        {
            return new SendMessageBatchRequestEntry(message.MessageId, message.Body)
            {
                MessageAttributes = message.MessageAttributes,
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                DelaySeconds = Convert.ToInt32(message.DelaySeconds)
            };
        }

        public static BatchEntry ToBatchRequest(this PreparedMessage message, Dictionary<string, PreparedMessage> batchEntries)
        {
            var preparedMessagesBydId = batchEntries.ToDictionary(x => x.Key, x => x.Value);
            var batchRequestEntries = new List<SendMessageBatchRequestEntry>(preparedMessagesBydId.Select(x => x.Value.ToBatchEntry()));

            return new BatchEntry
            {
                BatchRequest = new SendMessageBatchRequest(message.QueueUrl, batchRequestEntries),
                PreparedMessagesBydId = preparedMessagesBydId
            };
        }
    }
}