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