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

        public RegionEndpoint Region
        {
            get
            {
                return _settings.Get<RegionEndpoint>(SqsTransportSettings.Keys.Region);
            }
        }

        public int MaxTTLDays
        {
            get
            {
                return _settings.GetOrDefault<int>(SqsTransportSettings.Keys.MaxTTLDays);
            }
        }

		public string S3BucketForLargeMessages
        {
            get
            {
                return _settings.GetOrDefault<string>(SqsTransportSettings.Keys.S3BucketForLargeMessages);
            }
        }

		public string S3KeyPrefix
        {
            get
            {
                return _settings.GetOrDefault<string>(SqsTransportSettings.Keys.S3KeyPrefix);
            }
        }
        
		public string QueueNamePrefix
        {
            get
            {
                return _settings.GetOrDefault<string>(SqsTransportSettings.Keys.QueueNamePrefix);
            }
        }

        public SqsCredentialSource CredentialSource
        {
            get
            {
                return _settings.GetOrDefault<SqsCredentialSource>(SqsTransportSettings.Keys.CredentialSource);
            }
        }
         
		public bool TruncateLongQueueNames
        {
            get
            {
                return _settings.GetOrDefault<bool>(SqsTransportSettings.Keys.TruncateLongQueueNames);
            }
        }

        public string ProxyHost
        {
            get
            {
                 return _settings.GetOrDefault<string>(SqsTransportSettings.Keys.ProxyHost);
            }
        }

        public int ProxyPort
        {
            get
            {
                return _settings.GetOrDefault<int>(SqsTransportSettings.Keys.ProxyPort);
            }
        }
    }
}
