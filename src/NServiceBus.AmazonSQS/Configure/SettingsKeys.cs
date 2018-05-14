namespace NServiceBus
{
    static class SettingsKeys
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string SqsClientFactory = Prefix + nameof(SqsClientFactory);
        public const string MaxTimeToLive = Prefix + nameof(MaxTimeToLive);
        public const string S3BucketForLargeMessages = Prefix + nameof(S3BucketForLargeMessages);
        public const string S3KeyPrefix = Prefix + nameof(S3KeyPrefix);
        public const string S3ClientFactory = Prefix + nameof(S3ClientFactory);
        public const string QueueNamePrefix = Prefix + nameof(QueueNamePrefix);
        public const string CredentialSource = Prefix + nameof(CredentialSource);
        public const string PreTruncateQueueNames = Prefix + nameof(PreTruncateQueueNames);
        public const string UnrestrictedDurationDelayedDeliveryQueueDelayTime = Prefix + nameof(UnrestrictedDurationDelayedDeliveryQueueDelayTime);
        public const string V1CompatibilityMode = Prefix + nameof(V1CompatibilityMode);
    }
}