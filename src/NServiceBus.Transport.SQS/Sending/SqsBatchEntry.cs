namespace NServiceBus.Transport.SQS;

using System.Collections.Generic;
using Amazon.SQS.Model;

readonly struct SqsBatchEntry(
    SendMessageBatchRequest batchRequest,
    Dictionary<string, SqsPreparedMessage> preparedMessagesBydId)
{
    public readonly SendMessageBatchRequest BatchRequest = batchRequest;
    public readonly Dictionary<string, SqsPreparedMessage> PreparedMessagesBydId = preparedMessagesBydId;
}