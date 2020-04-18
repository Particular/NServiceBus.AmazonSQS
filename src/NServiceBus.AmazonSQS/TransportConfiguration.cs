namespace NServiceBus.Transport.AmazonSQS
{
    using System;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Configure;
    using Settings;
    using Unicast.Messages;

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

        public Func<IAmazonSimpleNotificationService> SnsClientFactory
        {
            get
            {
                if (snsClientFactory == null)
                {
                    snsClientFactory = settings.GetOrDefault<Func<IAmazonSimpleNotificationService>>(SettingsKeys.SnsClientFactory) ?? (() => new AmazonSimpleNotificationServiceClient());
                }
                return snsClientFactory;
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

        public string TopicNamePrefix
        {
            get
            {
                if (topicNamePrefix == null)
                {
                    topicNamePrefix = settings.GetOrDefault<string>(SettingsKeys.TopicNamePrefix);
                }
                return topicNamePrefix;
            }
        }

        public Func<MessageMetadata, string> TopicNameGenerator
        {
            get
            {
                if (topicNameGenerator == null)
                {
                    topicNameGenerator = metadata => (settings.GetOrDefault<Func<Type, string, string>>(SettingsKeys.TopicNameGenerator) ?? ((eventType, prefix) => TopicNameHelper.GetSnsTopicName(eventType, TopicNamePrefix, PreTruncateTopicNames)))(metadata.MessageType, TopicNamePrefix);
                }
                return topicNameGenerator;
            }
        }

        public bool PreTruncateTopicNames
        {
            get
            {
                if (!preTruncateTopicNames.HasValue)
                {
                    preTruncateTopicNames = settings.GetOrDefault<bool>(SettingsKeys.PreTruncateTopicNames);
                }
                return preTruncateTopicNames.Value;
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

        public ServerSideEncryptionMethod ServerSideEncryptionMethod
        {
            get
            {
                if (!serverSideEncryptionMethodInitialized)
                {
                    serverSideEncryptionMethod = settings.GetOrDefault<ServerSideEncryptionMethod>(SettingsKeys.ServerSideEncryptionMethod);
                    serverSideEncryptionMethodInitialized = true;
                }

                return serverSideEncryptionMethod;
            }
        }

        public string ServerSideEncryptionKeyManagementServiceKeyId
        {
            get
            {
                if (!serverSideEncryptionKeyManagementServiceKeyIdInitialized)
                {
                    serverSideEncryptionKeyManagementServiceKeyId = settings.GetOrDefault<string>(SettingsKeys.ServerSideEncryptionKeyManagementServiceKeyId);
                    serverSideEncryptionKeyManagementServiceKeyIdInitialized = true;
                }

                return serverSideEncryptionKeyManagementServiceKeyId;
            }
        }

        public ServerSideEncryptionCustomerMethod ServerSideEncryptionCustomerMethod
        {
            get
            {
                if (!serverSideEncryptionCustomerMethodInitialized)
                {
                    serverSideEncryptionCustomerMethod = settings.GetOrDefault<ServerSideEncryptionCustomerMethod>(SettingsKeys.ServerSideEncryptionCustomerMethod);
                    serverSideEncryptionCustomerMethodInitialized = true;
                }

                return serverSideEncryptionCustomerMethod;
            }
        }

        public string ServerSideEncryptionCustomerProvidedKey
        {
            get
            {
                if (!serverSideEncryptionCustomerProvidedKeyInitialized)
                {
                    serverSideEncryptionCustomerProvidedKey = settings.GetOrDefault<string>(SettingsKeys.ServerSideEncryptionCustomerProvidedKey);
                    serverSideEncryptionCustomerProvidedKeyInitialized = true;
                }

                return serverSideEncryptionCustomerProvidedKey;
            }
        }

        public string ServerSideEncryptionCustomerProvidedKeyMD5
        {
            get
            {
                if (!serverSideEncryptionCustomerProvidedKeyMD5Initialized)
                {
                    serverSideEncryptionCustomerProvidedKeyMD5 = settings.GetOrDefault<string>(SettingsKeys.ServerSideEncryptionCustomerProvidedKeyMD5);
                    serverSideEncryptionCustomerProvidedKeyMD5Initialized = true;
                }

                return serverSideEncryptionCustomerProvidedKeyMD5;
            }
        }

        public EventToTopicsMappings CustomEventToTopicsMappings => settings.GetOrDefault<EventToTopicsMappings>();
        public EventToEventsMappings CustomEventToEventsMappings => settings.GetOrDefault<EventToEventsMappings>();

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
        string topicNamePrefix;
        Func<MessageMetadata, string> topicNameGenerator;
        ServerSideEncryptionMethod serverSideEncryptionMethod;
        bool serverSideEncryptionMethodInitialized;
        string serverSideEncryptionKeyManagementServiceKeyId;
        bool serverSideEncryptionKeyManagementServiceKeyIdInitialized;
        ServerSideEncryptionCustomerMethod serverSideEncryptionCustomerMethod;
        bool serverSideEncryptionCustomerMethodInitialized;
        string serverSideEncryptionCustomerProvidedKey;
        bool serverSideEncryptionCustomerProvidedKeyInitialized;
        string serverSideEncryptionCustomerProvidedKeyMD5;
        bool serverSideEncryptionCustomerProvidedKeyMD5Initialized;
        bool? isDelayedDeliveryEnabled;
        bool? preTruncateQueueNames;
        bool? preTruncateTopicNames;
        bool? useV1CompatiblePayload;
        int? queueDelayTime;
        Func<IAmazonS3> s3ClientFactory;
        Func<IAmazonSQS> sqsClientFactory;
        Func<IAmazonSimpleNotificationService> snsClientFactory;
    }
}