namespace NServiceBus.Transports.SQS
{
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
                DelaySeconds = message.DelaySeconds
            };
        }

        static SendMessageBatchRequestEntry ToBatchEntry(this PreparedMessage message, string batchEntryId)
        {
            return new SendMessageBatchRequestEntry(batchEntryId, message.Body)
            {
                MessageAttributes = message.MessageAttributes,
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                DelaySeconds = message.DelaySeconds
            };
        }

        public static BatchEntry ToBatchRequest(this PreparedMessage message, Dictionary<string, PreparedMessage> batchEntries)
        {
            var preparedMessagesBydId = batchEntries.ToDictionary(x => x.Key, x => x.Value);

            var batchRequestEntries = new List<SendMessageBatchRequestEntry>();
            foreach (var kvp in preparedMessagesBydId)
            {
                batchRequestEntries.Add(kvp.Value.ToBatchEntry(kvp.Key));
            }

            return new BatchEntry
            {
                BatchRequest = new SendMessageBatchRequest(message.QueueUrl, batchRequestEntries),
                PreparedMessagesBydId = preparedMessagesBydId
            };
        }
    }
}