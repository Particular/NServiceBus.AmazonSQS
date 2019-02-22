namespace NServiceBus.AmazonSQS
{
    using System;
    using Amazon.S3;
    using Amazon.SQS;
    using Settings;

    class TransportConfiguration
    {
        public TransportConfiguration(ReadOnlySettings settings)
        {
            // Accessing the settings bag during runtime means a lot of boxing and unboxing,
            // all properties of this class are lazy initialized once they are accessed
            this.settings = settings;
        }

        public Func<IAmazonSQS> SqsClientFactory
        {
            get
            {
                if (sqsClientFactory == null)
                {
                    sqsClientFactory = settings.GetOrDefault<Func<IAmazonSQS>>(SettingsKeys.SqsClientFactory) ?? (() => new AmazonSQSClient());
                }
                return sqsClientFactory;
            }
        }

        public TimeSpan MaxTimeToLive
        {
            get
            {
                if (!maxTTL.HasValue)
                {
                    maxTTL = settings.GetOrDefault<TimeSpan>(SettingsKeys.MaxTimeToLive);
                }
                return maxTTL.Value;
            }
        }

        public Func<IAmazonS3> S3ClientFactory
        {
            get
            {
                if (s3ClientFactory == null)
                {
                    s3ClientFactory = settings.GetOrDefault<Func<IAmazonS3>>(SettingsKeys.S3ClientFactory) ?? (() => new AmazonS3Client());
                }
                return s3ClientFactory;
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

        public bool IsDelayedDeliveryEnabled
        {
            get
            {
                if (!isDelayedDeliveryEnabled.HasValue)
                {
                    isDelayedDeliveryEnabled = settings.HasSetting(SettingsKeys.UnrestrictedDurationDelayedDeliveryQueueDelayTime);
                }

                return isDelayedDeliveryEnabled.Value;
            }
        }

        public int DelayedDeliveryQueueDelayTime
        {
            get
            {
                if (!queueDelayTime.HasValue)
                {
                    queueDelayTime = settings.Get<int>(SettingsKeys.UnrestrictedDurationDelayedDeliveryQueueDelayTime);
                }

                return queueDelayTime.Value;
            }
        }

        public const string DelayedDeliveryQueueSuffix = "-delay.fifo";
        public static readonly int AwsMaximumQueueDelayTime = (int)TimeSpan.FromMinutes(15).TotalSeconds;
        public static readonly TimeSpan DelayedDeliveryQueueMessageRetentionPeriod = TimeSpan.FromDays(4);
        public const int MaximumMessageSize = 256 * 1024;
        public const int MaximumItemsInBatch = 10;

        ReadOnlySettings settings;
        TimeSpan? maxTTL;
        string s3BucketForLargeMessages;
        string s3KeyPrefix;
        string queueNamePrefix;
        bool? isDelayedDeliveryEnabled;
        bool? preTruncateQueueNames;
        bool? useV1CompatiblePayload;
        int? queueDelayTime;
        Func<IAmazonS3> s3ClientFactory;
        Func<IAmazonSQS> sqsClientFactory;
    }
}