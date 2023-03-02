namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SimpleNotificationService.Model;

    static class SnsPreparedMessageExtensions
    {
        public static PublishRequest ToPublishRequest(this SnsPreparedMessage message) =>
            new(message.Destination, message.Body)
            {
                MessageAttributes = message.MessageAttributes.ToDictionary(x => x.Key, x => new MessageAttributeValue
                {
                    DataType = x.Value.DataType,
                    StringValue = x.Value.StringValue,
                    BinaryValue = x.Value.BinaryValue,
                })
            };

        static PublishBatchRequestEntry ToBatchEntry(this SnsPreparedMessage message, string batchEntryId) =>
            new()
            {
                MessageAttributes = message.MessageAttributes,
                Id = batchEntryId,
                Message = message.Body
            };

        public static SnsBatchEntry ToBatchRequest(this SnsPreparedMessage message, Dictionary<string, SnsPreparedMessage> batchEntries)
        {
            var preparedMessagesBydId = batchEntries.ToDictionary(x => x.Key, x => x.Value);

            var batchRequestEntries = new List<PublishBatchRequestEntry>(batchEntries.Count);
            foreach (var kvp in preparedMessagesBydId)
            {
                batchRequestEntries.Add(kvp.Value.ToBatchEntry(kvp.Key));
            }

            return new SnsBatchEntry(
                new PublishBatchRequest
                {
                    TopicArn = message.Destination,
                    PublishBatchRequestEntries = batchRequestEntries
                },
                preparedMessagesBydId);
        }
    }
}