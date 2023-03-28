#nullable enable

namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SQS.Model;
    using MessageAttributeValue = Amazon.SQS.Model.MessageAttributeValue;

    static class SqsPreparedMessageExtensions
    {
        public static SendMessageRequest ToRequest(this SqsPreparedMessage message) =>
            new(message.QueueUrl, message.Body)
            {
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                MessageAttributes = message.MessageAttributes,
                DelaySeconds = message.DelaySeconds
            };

        public static void CopyMessageAttributes(this SqsPreparedMessage message, Dictionary<string, MessageAttributeValue>? nativeMessageAttributes)
        {
            foreach (var messageAttribute in nativeMessageAttributes ??
                                             Enumerable.Empty<KeyValuePair<string, MessageAttributeValue>>())
            {
                if (message.MessageAttributes.ContainsKey(messageAttribute.Key))
                {
                    continue;
                }

                message.MessageAttributes.Add(messageAttribute.Key, messageAttribute.Value);
            }
        }

        /// <summary>
        /// When receiving native messages, we expect some message attributes to be available in order to consume the message
        /// Once we've been able to process a native message *once*, we move those message attributes into the header collection we use across the framework
        /// </summary>
        public static void RemoveNativeHeaders(this SqsPreparedMessage message)
        {
            // We're removing these message attributes as we've copied them over into the header dictionary
            message.MessageAttributes.Remove(TransportHeaders.MessageTypeFullName);
            message.MessageAttributes.Remove(TransportHeaders.S3BodyKey);
        }

        static SendMessageBatchRequestEntry ToBatchEntry(this SqsPreparedMessage message, string batchEntryId) =>
            new(batchEntryId, message.Body)
            {
                MessageAttributes = message.MessageAttributes,
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                DelaySeconds = message.DelaySeconds
            };

        public static SqsBatchEntry ToBatchRequest(this SqsPreparedMessage message, Dictionary<string, SqsPreparedMessage> batchEntries)
        {
            var preparedMessagesBydId = batchEntries.ToDictionary(x => x.Key, x => x.Value);

            var batchRequestEntries = new List<SendMessageBatchRequestEntry>(batchEntries.Count);
            foreach (var kvp in preparedMessagesBydId)
            {
                batchRequestEntries.Add(kvp.Value.ToBatchEntry(kvp.Key));
            }

            return new SqsBatchEntry(
                new SendMessageBatchRequest(message.QueueUrl, batchRequestEntries),
                preparedMessagesBydId);
        }
    }
}