namespace NServiceBus.AmazonSQS
{
    using Amazon;
    using Settings;

    class SqsConnectionConfiguration
    {
        public SqsConnectionConfiguration(ReadOnlySettings settings)
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
                    region = settings.Get<RegionEndpoint>(SqsTransportSettingsKeys.Region);
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
                    maxTtlDays = settings.GetOrDefault<int>(SqsTransportSettingsKeys.MaxTTLDays);
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
                    s3BucketForLargeMessages = settings.GetOrDefault<string>(SqsTransportSettingsKeys.S3BucketForLargeMessages);
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
                    s3KeyPrefix = settings.GetOrDefault<string>(SqsTransportSettingsKeys.S3KeyPrefix);
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
                    queueNamePrefix = settings.GetOrDefault<string>(SqsTransportSettingsKeys.QueueNamePrefix);
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
                    credentialSource = settings.GetOrDefault<SqsCredentialSource>(SqsTransportSettingsKeys.CredentialSource);
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
                    proxyHost = settings.GetOrDefault<string>(SqsTransportSettingsKeys.ProxyHost);
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
                    proxyPort = settings.GetOrDefault<int>(SqsTransportSettingsKeys.ProxyPort);
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
                    nativeDeferral = settings.GetOrDefault<bool>(SqsTransportSettingsKeys.NativeDeferral);
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
                    preTruncateQueueNames = settings.GetOrDefault<bool>(SqsTransportSettingsKeys.PreTruncateQueueNames);
                }
                return preTruncateQueueNames.Value;
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
    }
}