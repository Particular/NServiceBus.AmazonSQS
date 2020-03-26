namespace NServiceBus
{
    static class SettingsKeys
    {
        const string Prefix = "NServiceBus.AmazonSQS.";
        public const string SqsClientFactory = Prefix + nameof(SqsClientFactory);
        public const string SnsClientFactory = Prefix + nameof(SnsClientFactory);
        public const string MaxTimeToLive = Prefix + nameof(MaxTimeToLive);
        public const string S3BucketForLargeMessages = Prefix + nameof(S3BucketForLargeMessages);
        public const string S3KeyPrefix = Prefix + nameof(S3KeyPrefix);
        public const string S3ClientFactory = Prefix + nameof(S3ClientFactory);
        public const string ServerSideEncryptionMethod = Prefix + nameof(ServerSideEncryptionMethod);
        public const string ServerSideEncryptionKeyManagementServiceKeyId = Prefix + nameof(ServerSideEncryptionKeyManagementServiceKeyId);
        public const string ServerSideEncryptionCustomerMethod = Prefix + nameof(ServerSideEncryptionCustomerMethod);
        public const string ServerSideEncryptionCustomerProvidedKey = Prefix + nameof(ServerSideEncryptionCustomerProvidedKey);
        public const string ServerSideEncryptionCustomerProvidedKeyMD5 = Prefix + nameof(ServerSideEncryptionCustomerProvidedKeyMD5);
        public const string QueueNamePrefix = Prefix + nameof(QueueNamePrefix);
        public const string TopicNamePrefix = Prefix + nameof(TopicNamePrefix);
        public const string CredentialSource = Prefix + nameof(CredentialSource);
        public const string PreTruncateQueueNames = Prefix + nameof(PreTruncateQueueNames);
        public const string PreTruncateTopicNames = Prefix + nameof(PreTruncateTopicNames);
        public const string UnrestrictedDurationDelayedDeliveryQueueDelayTime = Prefix + nameof(UnrestrictedDurationDelayedDeliveryQueueDelayTime);
        public const string V1CompatibilityMode = Prefix + nameof(V1CompatibilityMode);
        public const string DisableNativePubSub = Prefix + nameof(DisableNativePubSub);
    }
}