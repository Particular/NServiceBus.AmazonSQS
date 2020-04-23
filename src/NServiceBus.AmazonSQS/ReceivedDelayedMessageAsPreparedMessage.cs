namespace NServiceBus.Transports.SQS
{
    class ReceivedDelayedMessageAsPreparedMessage : PreparedMessage
    {
        public ReceivedDelayedMessageAsPreparedMessage(string receivedMessageId, string receiptHandle)
        {
            ReceivedMessageId = receivedMessageId;
            ReceiptHandle = receiptHandle;
        }

        public string ReceivedMessageId { get; }
        public string ReceiptHandle { get; }
    }
}