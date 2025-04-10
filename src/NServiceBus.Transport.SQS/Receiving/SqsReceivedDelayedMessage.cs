#nullable enable

namespace NServiceBus.Transport.SQS;

class SqsReceivedDelayedMessage(string receivedMessageId, string receiptHandle) : SqsPreparedMessage
{
    public string ReceivedMessageId { get; } = receivedMessageId;
    public string ReceiptHandle { get; } = receiptHandle;
}