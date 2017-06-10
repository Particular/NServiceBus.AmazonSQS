namespace NServiceBus
{
    static class SqsTransportSettingsKeys
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string Region = Prefix + nameof(Region);
        public const string MaxTTLDays = Prefix + nameof(MaxTTLDays);
        public const string S3BucketForLargeMessages = Prefix + nameof(S3BucketForLargeMessages);
        public const string S3KeyPrefix = Prefix + nameof(S3KeyPrefix);
        public const string QueueNamePrefix = Prefix + nameof(QueueNamePrefix);
        public const string CredentialSource = Prefix + nameof(CredentialSource);
        public const string ProxyHost = Prefix + nameof(ProxyHost);
        public const string ProxyPort = Prefix + nameof(ProxyPort);
        public const string NativeDeferral = Prefix + nameof(NativeDeferral);
        public const string PreTruncateQueueNames = Prefix + nameof(PreTruncateQueueNames);
    }
}
