namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensions;
    using Logging;

    class QueueCreator
    {
        public QueueCreator(IAmazonSQS sqsClient, QueueCache queueCache, S3Settings s3Settings, TimeSpan maxTimeToLive, int queueDelaySeconds)
        {
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
            this.s3Settings = s3Settings;
            this.maxTimeToLive = maxTimeToLive;
            this.queueDelaySeconds = queueDelaySeconds;
        }

        public async Task CreateQueueIfNecessary(string address, bool createDelayedDeliveryQueue, CancellationToken cancellationToken = default)
        {
            try
            {
                var queueName = address;
                var delayDeliveryQueuePhysicalAddress = queueCache.GetPhysicalQueueName(queueName);
                var sqsRequest = new CreateQueueRequest
                {
                    QueueName = delayDeliveryQueuePhysicalAddress
                };

                Logger.Info($"Creating SQS Queue with name '{sqsRequest.QueueName}' for address '{queueName}'.");
                var createQueueResponse = await sqsClient.CreateQueueAsync(sqsRequest, cancellationToken).ConfigureAwait(false);

                queueCache.SetQueueUrl(queueName, createQueueResponse.QueueUrl);

                // Set the queue attributes in a separate call.
                // If you call CreateQueue with a queue name that already exists, and with a different
                // value for MessageRetentionPeriod, the service throws. This will happen if you
                // change the MaxTTLDays configuration property.
                var sqsAttributesRequest = new SetQueueAttributesRequest
                {
                    QueueUrl = createQueueResponse.QueueUrl
                };
                sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod,
                    maxTimeToLive.TotalSeconds.ToString(CultureInfo.InvariantCulture));

                await sqsClient.SetQueueAttributesAsync(sqsAttributesRequest, cancellationToken).ConfigureAwait(false);

                if (createDelayedDeliveryQueue)
                {
                    var delayedDeliveryQueueName = $"{queueName}{TransportConstraints.DelayedDeliveryQueueSuffix}";
                    delayDeliveryQueuePhysicalAddress = queueCache.GetPhysicalQueueName(delayedDeliveryQueueName);
                    sqsRequest = new CreateQueueRequest
                    {
                        QueueName = delayDeliveryQueuePhysicalAddress,
                        Attributes = new Dictionary<string, string> { { "FifoQueue", "true" } }
                    };

                    Logger.Info($"Creating SQS delayed delivery queue with name '{sqsRequest.QueueName}' for address '{address}'.");
                    createQueueResponse = await sqsClient.CreateQueueAsync(sqsRequest, cancellationToken).ConfigureAwait(false);

                    queueCache.SetQueueUrl(delayedDeliveryQueueName, createQueueResponse.QueueUrl);

                    sqsAttributesRequest = new SetQueueAttributesRequest
                    {
                        QueueUrl = createQueueResponse.QueueUrl
                    };

                    sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod, TransportConstraints.DelayedDeliveryQueueMessageRetentionPeriod.TotalSeconds.ToString(CultureInfo.InvariantCulture));
                    sqsAttributesRequest.Attributes.Add(QueueAttributeName.DelaySeconds, queueDelaySeconds.ToString(CultureInfo.InvariantCulture));

                    await sqsClient.SetQueueAttributesAsync(sqsAttributesRequest, cancellationToken).ConfigureAwait(false);
                }

                if (s3Settings != null)
                {
                    // determine if the configured bucket exists; create it if it doesn't
                    var listBucketsResponse = await s3Settings.S3Client.ListBucketsAsync(new ListBucketsRequest(), cancellationToken).ConfigureAwait(false);
                    var bucketExists = listBucketsResponse.Buckets.Any(x => string.Equals(x.BucketName, s3Settings.BucketName, StringComparison.InvariantCultureIgnoreCase));
                    if (!bucketExists)
                    {
                        await s3Settings.S3Client.RetryConflictsAsync(async token =>
                                await s3Settings.S3Client.PutBucketAsync(new PutBucketRequest
                                {
                                    BucketName = s3Settings.BucketName
                                }, token).ConfigureAwait(false), onRetry: x => { Logger.Warn($"Conflict when creating S3 bucket, retrying after {x}ms."); }, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }

                    var lifecycleConfig = await s3Settings.S3Client.GetLifecycleConfigurationAsync(s3Settings.BucketName, cancellationToken).ConfigureAwait(false);
                    var setLifecycleConfig = lifecycleConfig.Configuration.Rules.All(x => x.Id != "NServiceBus.SQS.DeleteMessageBodies");

                    if (setLifecycleConfig)
                    {
                        await s3Settings.S3Client.RetryConflictsAsync(async token =>
                                await s3Settings.S3Client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                                {
                                    BucketName = s3Settings.BucketName,
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
                                                        Prefix = s3Settings.KeyPrefix
                                                    }
                                                },
                                                Status = LifecycleRuleStatus.Enabled,
                                                Expiration = new LifecycleRuleExpiration
                                                {
                                                    Days = (int)Math.Ceiling(maxTimeToLive.TotalDays)
                                                }
                                            }
                                        }
                                    }
                                }, token).ConfigureAwait(false), onRetry: x => { Logger.Warn($"Conflict when setting S3 lifecycle configuration, retrying after {x}ms."); }, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Logger.Error("Exception from CreateQueueIfNecessary.", ex);
                throw;
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(QueueCreator));
        IAmazonSQS sqsClient;
        QueueCache queueCache;
        readonly S3Settings s3Settings;
        readonly TimeSpan maxTimeToLive;
        readonly int queueDelaySeconds;
    }
}