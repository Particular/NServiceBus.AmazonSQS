namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using AmazonSQS;
    using Logging;
    using Transport;

    class QueueCreator : ICreateQueues
    {
        public QueueCreator(ConnectionConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache, bool isDelayedDeliveryEnabled)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
            this.isDelayedDeliveryEnabled = isDelayedDeliveryEnabled;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var tasks = new List<Task>();

            foreach (var address in queueBindings.SendingAddresses)
            {
                tasks.Add(CreateQueueIfNecessary(address, false));
            }
            foreach (var address in queueBindings.ReceivingAddresses)
            {
                tasks.Add(CreateQueueIfNecessary(address, true));
            }
            return Task.WhenAll(tasks);
        }

        async Task CreateQueueIfNecessary(string address, bool createDelayedDeliveryQueue)
        {
            try
            {
                var queueName = QueueNameHelper.GetSqsQueueName(address, configuration);
                var sqsRequest = new CreateQueueRequest
                {
                    QueueName = queueName
                };

                Logger.Info($"Creating SQS Queue with name '{sqsRequest.QueueName}' for address '{address}'.");
                var createQueueResponse = await sqsClient.CreateQueueAsync(sqsRequest).ConfigureAwait(false);

                queueUrlCache.SetQueueUrl(queueName, createQueueResponse.QueueUrl);

                // Set the queue attributes in a separate call.
                // If you call CreateQueue with a queue name that already exists, and with a different
                // value for MessageRetentionPeriod, the service throws. This will happen if you
                // change the MaxTTLDays configuration property.
                var sqsAttributesRequest = new SetQueueAttributesRequest
                {
                    QueueUrl = createQueueResponse.QueueUrl
                };
                sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod,
                    configuration.MaxTimeToLive.TotalSeconds.ToString());

                await sqsClient.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);

                if (isDelayedDeliveryEnabled && createDelayedDeliveryQueue)
                {
                    queueName = QueueNameHelper.GetSqsQueueName(address, configuration) + "-delay.fifo";
                    sqsRequest = new CreateQueueRequest
                    {
                        QueueName = queueName,
                        Attributes = new Dictionary<string, string> { {"FifoQueue", "true"} }
                    };

                    Logger.Info($"Creating SQS delayed delivery queue with name '{sqsRequest.QueueName}' for address '{address}'.");
                    createQueueResponse = await sqsClient.CreateQueueAsync(sqsRequest).ConfigureAwait(false);

                    queueUrlCache.SetQueueUrl(queueName, createQueueResponse.QueueUrl);

                    sqsAttributesRequest = new SetQueueAttributesRequest
                    {
                        QueueUrl = createQueueResponse.QueueUrl
                    };
                    // TTL on messages in .fifo queue should not be less than maximum AWS delay time (15 minutes). 4 days is the default upon queue creation.
                    sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod, TimeSpan.FromDays(4).TotalSeconds.ToString());

                    await sqsClient.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(configuration.S3BucketForLargeMessages))
                {
                    // determine if the configured bucket exists; create it if it doesn't
                    var listBucketsResponse = await s3Client.ListBucketsAsync(new ListBucketsRequest()).ConfigureAwait(false);
                    var bucketExists = listBucketsResponse.Buckets.Any(x => string.Equals(x.BucketName, configuration.S3BucketForLargeMessages, StringComparison.InvariantCultureIgnoreCase));
                    if (!bucketExists)
                    {
                        await s3Client.RetryConflictsAsync(async () =>
                                await s3Client.PutBucketAsync(new PutBucketRequest
                                {
                                    BucketName = configuration.S3BucketForLargeMessages
                                }).ConfigureAwait(false),
                            onRetry: x => { Logger.Warn($"Conflict when creating S3 bucket, retrying after {x}ms."); }).ConfigureAwait(false);
                    }

                    var lifecycleConfig = await s3Client.GetLifecycleConfigurationAsync(configuration.S3BucketForLargeMessages).ConfigureAwait(false);
                    var setLifecycleConfig = lifecycleConfig.Configuration.Rules.All(x => x.Id != "NServiceBus.SQS.DeleteMessageBodies");

                    if (setLifecycleConfig)
                    {
                        await s3Client.RetryConflictsAsync(async () =>
                                await s3Client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                                {
                                    BucketName = configuration.S3BucketForLargeMessages,
                                    Configuration = new LifecycleConfiguration
                                    {
                                        Rules = new List<LifecycleRule>
                                        {
                                            new LifecycleRule
                                            {
                                                Id = "NServiceBus.SQS.DeleteMessageBodies",
                                                Filter = new LifecycleFilter
                                                {
                                                    LifecycleFilterPredicate = new LifecyclePrefixPredicate
                                                    {
                                                        Prefix = configuration.S3KeyPrefix
                                                    }
                                                },
                                                Status = LifecycleRuleStatus.Enabled,
                                                Expiration = new LifecycleRuleExpiration
                                                {
                                                    Days = (int)Math.Ceiling(configuration.MaxTimeToLive.TotalDays)
                                                }
                                            }
                                        }
                                    }
                                }).ConfigureAwait(false),
                            onRetry: x => { Logger.Warn($"Conflict when setting S3 lifecycle configuration, retrying after {x}ms."); }).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Exception from CreateQueueIfNecessary.", e);
                throw;
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(QueueCreator));
        ConnectionConfiguration configuration;
        IAmazonS3 s3Client;
        IAmazonSQS sqsClient;
        QueueUrlCache queueUrlCache;
        readonly bool isDelayedDeliveryEnabled;
    }
}