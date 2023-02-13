namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    readonly struct SqsBatchEntry
    {
        public readonly SendMessageBatchRequest BatchRequest;
        public readonly Dictionary<string, SqsPreparedMessage> PreparedMessagesBydId;

        public SqsBatchEntry(SendMessageBatchRequest batchRequest, Dictionary<string, SqsPreparedMessage> preparedMessagesBydId)
        {
            BatchRequest = batchRequest;
            PreparedMessagesBydId = preparedMessagesBydId;
        }
    }
}