namespace NServiceBus.Transports.SQS
{
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NServiceBus.AmazonSQS;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Logging;
    using Transport;
    using System.Threading.Tasks;

    class SqsQueueCreator : ICreateQueues
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            try
            {
                var sqsRequest = new CreateQueueRequest
                {
                    QueueName = address.ToSqsQueueName(ConnectionConfiguration),
                };
                Logger.Info(String.Format("Creating SQS Queue with name \"{0}\" for address \"{1}\".", sqsRequest.QueueName, address));
                var createQueueResponse = SqsClient.CreateQueue(sqsRequest);

                // Set the queue attributes in a separate call. 
                // If you call CreateQueue with a queue name that already exists, and with a different
                // value for MessageRetentionPeriod, the service throws. This will happen if you 
                // change the MaxTTLDays configuration property. 
                var sqsAttributesRequest = new SetQueueAttributesRequest
                {
                    QueueUrl = createQueueResponse.QueueUrl
                };
                sqsAttributesRequest.Attributes.Add(QueueAttributeName.MessageRetentionPeriod,
                    ((int) (TimeSpan.FromDays(ConnectionConfiguration.MaxTTLDays).TotalSeconds)).ToString());

                SqsClient.SetQueueAttributes(sqsAttributesRequest);

                if (!string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
                {
                    // determine if the configured bucket exists; create it if it doesn't
                    var listBucketsResponse = S3Client.ListBuckets(new ListBucketsRequest());
                    var bucketExists = listBucketsResponse.Buckets.Any(x => x.BucketName.ToLower() == ConnectionConfiguration.S3BucketForLargeMessages.ToLower());
                    if (!bucketExists)
                    {
                        S3Client.RetryConflicts(() =>
                        {
                            return S3Client.PutBucket(new PutBucketRequest
                            {
                                BucketName = ConnectionConfiguration.S3BucketForLargeMessages
                            });
                        },
                        onRetry: x =>
                        {
                            Logger.Warn($"Conflict when creating S3 bucket, retrying after {x}ms.");
                        });
                    }

                    S3Client.RetryConflicts(() =>
                    {
                        return S3Client.PutLifecycleConfiguration(new PutLifecycleConfigurationRequest
                        {
                            BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                            Configuration = new LifecycleConfiguration
                            {
                                Rules = new List<LifecycleRule>
                                {
                                    new LifecycleRule
                                    {
                                        Id = "NServiceBus.SQS.DeleteMessageBodies",
                                        Filter = new LifecycleFilter()
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
                        });
                    },
                    onRetry: x =>
                    {
                        Logger.Warn($"Conflict when setting S3 lifecycle configuration, retrying after {x}ms.");
                    });
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
