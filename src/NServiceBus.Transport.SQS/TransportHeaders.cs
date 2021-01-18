namespace NServiceBus.Transport.SQS
{
    static class TransportHeaders
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string TimeToBeReceived = Prefix + nameof(TimeToBeReceived);
        public const string DelaySeconds = Prefix + nameof(DelaySeconds);
        public const string S3BodyKey = "S3BodyKey";
        public const string MessageTypeFullName = "MessageTypeFullName";
    }
}