namespace NServiceBus.Transport.SQS
{
    class SqsReceivedDelayedMessage : SqsPreparedMessage
    {
        public SqsReceivedDelayedMessage(string receivedMessageId, string receiptHandle)
        {
            ReceivedMessageId = receivedMessageId;
            ReceiptHandle = receiptHandle;
        }

        public string ReceivedMessageId { get; }
        public string ReceiptHandle { get; }
    }
}