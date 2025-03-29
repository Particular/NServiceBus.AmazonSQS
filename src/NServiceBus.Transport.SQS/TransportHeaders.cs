namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;

    static class TransportHeaders
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string TimeToBeReceived = Prefix + nameof(TimeToBeReceived);
        public const string DelaySeconds = Prefix + nameof(DelaySeconds);
        public const string Headers = Prefix + nameof(Headers);
        public const string S3BodyKey = "S3BodyKey";
        public const string MessageTypeFullName = "MessageTypeFullName";
        public static HashSet<string> AllTransportHeaders = [TimeToBeReceived, DelaySeconds, Headers, S3BodyKey, MessageTypeFullName];
    }
}