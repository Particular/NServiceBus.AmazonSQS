namespace NServiceBus.AmazonSQS
{
    using Amazon;
    using Settings;

    internal class SqsConnectionConfiguration
    {
        private SettingsHolder _settings;

        public SqsConnectionConfiguration(SettingsHolder settingsHolder)
        {
            _settings = settingsHolder;
        }

        public RegionEndpoint Region => _settings.Get<RegionEndpoint>(SqsTransportSettingsKeys.Region);

        public int MaxTTLDays => _settings.GetOrDefault<int>(SqsTransportSettingsKeys.MaxTTLDays);

        public string S3BucketForLargeMessages => _settings.GetOrDefault<string>(SqsTransportSettingsKeys.S3BucketForLargeMessages);

        public string S3KeyPrefix => _settings.GetOrDefault<string>(SqsTransportSettingsKeys.S3KeyPrefix);

        public string QueueNamePrefix => _settings.GetOrDefault<string>(SqsTransportSettingsKeys.QueueNamePrefix);

        public SqsCredentialSource CredentialSource => _settings.GetOrDefault<SqsCredentialSource>(SqsTransportSettingsKeys.CredentialSource);

        public string ProxyHost => _settings.GetOrDefault<string>(SqsTransportSettingsKeys.ProxyHost);

        public int ProxyPort => _settings.GetOrDefault<int>(SqsTransportSettingsKeys.ProxyPort);

        public bool NativeDeferral => _settings.GetOrDefault<bool>(SqsTransportSettingsKeys.NativeDeferral);

        public bool PreTruncateQueueNames => _settings.GetOrDefault<bool>(SqsTransportSettingsKeys.PreTruncateQueueNames);
    }
}
