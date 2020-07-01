namespace NServiceBus.Transports.SQS
{
    class SqsReceivedDelayedMessage : PreparedMessage
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