namespace NServiceBus.AmazonSQS
{
    static class TransportHeaders
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string TimeToBeReceived = Prefix + nameof(TimeToBeReceived);
        public const string DelaySeconds = Prefix + nameof(DelaySeconds);
    }
}