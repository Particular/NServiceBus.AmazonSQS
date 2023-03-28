namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SimpleNotificationService.Model;

    readonly struct SnsBatchEntry
    {
        public readonly PublishBatchRequest BatchRequest;
        public readonly Dictionary<string, SnsPreparedMessage> PreparedMessagesBydId;

        public SnsBatchEntry(PublishBatchRequest batchRequest, Dictionary<string, SnsPreparedMessage> preparedMessagesBydId)
        {
            BatchRequest = batchRequest;
            PreparedMessagesBydId = preparedMessagesBydId;
        }
    }
}