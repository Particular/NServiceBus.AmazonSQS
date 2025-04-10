namespace NServiceBus.Transport.SQS;

using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

readonly struct SnsBatchEntry(
    PublishBatchRequest batchRequest,
    Dictionary<string, SnsPreparedMessage> preparedMessagesBydId)
{
    public readonly PublishBatchRequest BatchRequest = batchRequest;
    public readonly Dictionary<string, SnsPreparedMessage> PreparedMessagesBydId = preparedMessagesBydId;
}