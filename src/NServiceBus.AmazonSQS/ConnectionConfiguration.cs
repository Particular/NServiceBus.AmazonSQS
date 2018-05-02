namespace NServiceBus.AmazonSQS
{
    using Amazon;
    using Settings;

    class ConnectionConfiguration
    {
        public ConnectionConfiguration(ReadOnlySettings settings)
        {
            // Accessing the settings bag during runtime means a lot of boxing and unboxing, 
            // all properties of this class are lazy initialized once they are accessed
            this.settings = settings;
        }

        public RegionEndpoint Region
        {
            get
            {
                if (region == null)
                {
                    region = settings.Get<RegionEndpoint>(SettingsKeys.Region);
                }
                return region;
            }
        }

        public int MaxTTLDays
        {
            get
            {
                if (!maxTtlDays.HasValue)
                {
                    maxTtlDays = settings.GetOrDefault<int>(SettingsKeys.MaxTTLDays);
                }
                return maxTtlDays.Value;
            }
        }

        public string S3BucketForLargeMessages
        {
            get
            {
                if (s3BucketForLargeMessages == null)
                {
                    s3BucketForLargeMessages = settings.GetOrDefault<string>(SettingsKeys.S3BucketForLargeMessages);
                }
                return s3BucketForLargeMessages;
            }
        }

        public string S3KeyPrefix
        {
            get
            {
                if (s3KeyPrefix == null)
                {
                    s3KeyPrefix = settings.GetOrDefault<string>(SettingsKeys.S3KeyPrefix);
                }
                return s3KeyPrefix;
            }
        }

        public string QueueNamePrefix
        {
            get
            {
                if (queueNamePrefix == null)
                {
                    queueNamePrefix = settings.GetOrDefault<string>(SettingsKeys.QueueNamePrefix);
                }
                return queueNamePrefix;
            }
        }

        public SqsCredentialSource CredentialSource
        {
            get
            {
                if (!credentialSource.HasValue)
                {
                    credentialSource = settings.GetOrDefault<SqsCredentialSource>(SettingsKeys.CredentialSource);
                }
                return credentialSource.Value;
            }
        }

        public string ProxyHost
        {
            get
            {
                if (proxyHost == null)
                {
                    proxyHost = settings.GetOrDefault<string>(SettingsKeys.ProxyHost);
                }
                return proxyHost;
            }
        }

        public int ProxyPort
        {
            get
            {
                if (!proxyPort.HasValue)
                {
                    proxyPort = settings.GetOrDefault<int>(SettingsKeys.ProxyPort);
                }
                return proxyPort.Value;
            }
        }

        public bool NativeDeferral
        {
            get
            {
                if (!nativeDeferral.HasValue)
                {
                    nativeDeferral = settings.GetOrDefault<bool>(SettingsKeys.NativeDeferral);
                }
                return nativeDeferral.Value;
            }
        }

        public bool PreTruncateQueueNames
        {
            get
            {
                if (!preTruncateQueueNames.HasValue)
                {
                    preTruncateQueueNames = settings.GetOrDefault<bool>(SettingsKeys.PreTruncateQueueNames);
                }
                return preTruncateQueueNames.Value;
            }
        }

        public bool UseV1CompatiblePayload
        {
            get
            {
                if (!useV1CompatiblePayload.HasValue)
                {
                    useV1CompatiblePayload = settings.GetOrDefault<bool>(SettingsKeys.V1CompatibilityMode);
                }

                return useV1CompatiblePayload.Value;
            }
        }

        RegionEndpoint region;
        ReadOnlySettings settings;
        int? maxTtlDays;
        string s3BucketForLargeMessages;
        string s3KeyPrefix;
        string queueNamePrefix;
        SqsCredentialSource? credentialSource;
        string proxyHost;
        int? proxyPort;
        bool? nativeDeferral;
        bool? preTruncateQueueNames;
        bool? useV1CompatiblePayload;
    }
}