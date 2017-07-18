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

    class SqsQueueCreator : ICreateQueues
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public SqsQueueUrlCache QueueUrlCache { get; set; }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var tasks = new List<Task>();

            foreach (var address in queueBindings.SendingAddresses)
            {
                tasks.Add(CreateQueueIfNecessary(address));
            }
            foreach (var address in queueBindings.ReceivingAddresses)
            {
                tasks.Add(CreateQueueIfNecessary(address));
            }
            return Task.WhenAll(tasks);
        }

        public async Task CreateQueueIfNecessary(string address)
        {
            try
            {
                var queueName = SqsQueueNameHelper.GetSqsQueueName(address, ConnectionConfiguration);
                var sqsRequest = new CreateQueueRequest
                {
                    QueueName = queueName
                };

                Logger.Info($"Creating SQS Queue with name \"{sqsRequest.QueueName}\" for address \"{address}\".");
                var createQueueResponse = await SqsClient.CreateQueueAsync(sqsRequest).ConfigureAwait(false);

                QueueUrlCache.SetQueueUrl(queueName, createQueueResponse.QueueUrl);

                // Set the queue attributes in a separate call.
                // If you call CreateQueue with a queue name that already exists, and with a different
                // value for MessageRetentionPeriod, the service throws. This will happen if you
                // change the MaxTTLDays configuration property.
                var sqsAttributesRequest = new SetQueueAttributesRequest
                {
                    QueueUrl = createQueueResponse.QueueUrl
                };
                sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod,
                    ((int)TimeSpan.FromDays(ConnectionConfiguration.MaxTTLDays).TotalSeconds).ToString());

                await SqsClient.SetQueueAttributesAsync(sqsAttributesRequest).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
                {
                    // determine if the configured bucket exists; create it if it doesn't
                    var listBucketsResponse = await S3Client.ListBucketsAsync(new ListBucketsRequest()).ConfigureAwait(false);
                    var bucketExists = listBucketsResponse.Buckets.Any(x => string.Equals(x.BucketName, ConnectionConfiguration.S3BucketForLargeMessages, StringComparison.InvariantCultureIgnoreCase));
                    if (!bucketExists)
                    {
                        await S3Client.RetryConflictsAsync(async () =>
                                await S3Client.PutBucketAsync(new PutBucketRequest
                                {
                                    BucketName = ConnectionConfiguration.S3BucketForLargeMessages
                                }).ConfigureAwait(false),
                            onRetry: x => { Logger.Warn($"Conflict when creating S3 bucket, retrying after {x}ms."); }).ConfigureAwait(false);
                    }

                    var lifecycleConfig = await S3Client.GetLifecycleConfigurationAsync(ConnectionConfiguration.S3BucketForLargeMessages).ConfigureAwait(false);
                    var setLifecycleConfig = lifecycleConfig.Configuration.Rules.All(x => x.Id != "NServiceBus.SQS.DeleteMessageBodies");

                    if (setLifecycleConfig)
                    {
                        await S3Client.RetryConflictsAsync(async () =>
                                await S3Client.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                                {
                                    BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
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
                                                        Prefix = ConnectionConfiguration.S3KeyPrefix
                                                    }
                                                },
                                                Status = LifecycleRuleStatus.Enabled,
                                                Expiration = new LifecycleRuleExpiration
                                                {
                                                    Days = ConnectionConfiguration.MaxTTLDays
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

        static ILog Logger = LogManager.GetLogger(typeof(SqsQueueCreator));
    }
}