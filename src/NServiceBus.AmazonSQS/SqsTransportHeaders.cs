namespace NServiceBus.AmazonSQS
{
    static class SqsTransportHeaders
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string TimeToBeReceived = Prefix + nameof(TimeToBeReceived);
    }
}