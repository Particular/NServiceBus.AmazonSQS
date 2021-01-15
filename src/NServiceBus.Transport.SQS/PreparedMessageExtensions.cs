namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS.Model;
    using SnsMessageAttributeValue = Amazon.SimpleNotificationService.Model.MessageAttributeValue;

    static class PreparedMessageExtensions
    {
        public static SendMessageRequest ToRequest(this SqsPreparedMessage message)
        {
            return new SendMessageRequest(message.QueueUrl, message.Body)
            {
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                MessageAttributes = message.MessageAttributes,
                DelaySeconds = message.DelaySeconds
            };
        }

        public static PublishRequest ToPublishRequest(this SnsPreparedMessage message)
        {
            return new PublishRequest(message.Destination, message.Body)
            {
                MessageAttributes = message.MessageAttributes.ToDictionary(x => x.Key, x => new SnsMessageAttributeValue
                {
                    DataType = x.Value.DataType,
                    StringValue = x.Value.StringValue,
                    BinaryValue = x.Value.BinaryValue,
                })
            };
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

        static SendMessageBatchRequestEntry ToBatchEntry(this SqsPreparedMessage message, string batchEntryId)
        {
            return new SendMessageBatchRequestEntry(batchEntryId, message.Body)
            {
                MessageAttributes = message.MessageAttributes,
                MessageGroupId = message.MessageGroupId,
                MessageDeduplicationId = message.MessageDeduplicationId,
                DelaySeconds = message.DelaySeconds
            };
        }

        public static BatchEntry<TMessage> ToBatchRequest<TMessage>(this TMessage message, Dictionary<string, TMessage> batchEntries)
            where TMessage : SqsPreparedMessage
        {
            var preparedMessagesBydId = batchEntries.ToDictionary(x => x.Key, x => x.Value);

            var batchRequestEntries = new List<SendMessageBatchRequestEntry>();
            foreach (var kvp in preparedMessagesBydId)
            {
                batchRequestEntries.Add(kvp.Value.ToBatchEntry(kvp.Key));
            }

            return new BatchEntry<TMessage>
            {
                BatchRequest = new SendMessageBatchRequest(message.QueueUrl, batchRequestEntries),
                PreparedMessagesBydId = preparedMessagesBydId
            };
        }
    }
}