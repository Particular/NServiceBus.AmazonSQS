namespace NServiceBus.Transport.SQS
{
    using System.Collections.Frozen;
    using System.Collections.Generic;

    static class TransportHeaders
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string TimeToBeReceived = Prefix + nameof(TimeToBeReceived);
        public const string DelaySeconds = Prefix + nameof(DelaySeconds);
        public const string Headers = Prefix + nameof(Headers);
        public const string S3BodyKey = "S3BodyKey";
        public const string MessageTypeFullName = "MessageTypeFullName";

        // The following set represents the list of native message attributes that will
        // not be copied to NServiceBus headers. When adding a new header to this class
        // consider if that needs to be propagated to NServiceBus headers, if not, add it
        // to this frozen set.
        public static FrozenSet<string> NativeMessageAttributesNotCopiedToNServiceBusHeaders = new HashSet<string>([TimeToBeReceived, DelaySeconds, Headers, S3BodyKey, MessageTypeFullName]).ToFrozenSet();
    }
}