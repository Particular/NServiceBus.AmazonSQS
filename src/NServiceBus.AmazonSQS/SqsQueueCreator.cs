namespace NServiceBus.Transports.SQS
{
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NServiceBus.AmazonSQS;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using NServiceBus.AmazonSQS.Extensions;
    using NServiceBus.Logging;

    class SqsQueueCreator : ICreateQueues
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public IAmazonSQS SqsClient { get; set; }
		
        public void CreateQueueIfNecessary(Address address, string account)
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

                CreateS3ResourcesIfNecessary();
            }
            catch (Exception e)
            {
                Logger.Error("Exception from CreateQueueIfNecessary.", e);
                throw;
            }
        }

        public const int MaxS3BucketRetries = 3;

        private void CreateS3ResourcesIfNecessary(int attemptCount = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
                {
                    return;
                }

                var bucketForLargeMessages = ConnectionConfiguration.S3BucketForLargeMessages;

                // determine if the configured bucket exists; create it if it doesn't

                if (S3Client.DoesS3BucketExist(bucketForLargeMessages))
                {
                    S3Client.PutBucket(new PutBucketRequest
                    {
                        BucketName = bucketForLargeMessages
                    });
                }

                var lifecycleConfigurationResponse = S3Client.GetLifecycleConfiguration(new GetLifecycleConfigurationRequest
                {
                    BucketName = bucketForLargeMessages
                });

                var lifeCycleRule = new LifecycleRule
                {
                    Id = "NServiceBus.SQS.DeleteMessageBodies",
                    Prefix = ConnectionConfiguration.S3KeyPrefix,
                    Status = LifecycleRuleStatus.Enabled,
                    Expiration = new LifecycleRuleExpiration
                    {
                        Days = ConnectionConfiguration.MaxTTLDays
                    }
                };

                if (!lifecycleConfigurationResponse.Configuration.ContainsMatchingRule(lifeCycleRule))
                {
                    S3Client.PutLifecycleConfiguration(new PutLifecycleConfigurationRequest
                    {
                        BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                        Configuration = new LifecycleConfiguration
                        {
                            Rules = new List<LifecycleRule>
                            {
                                lifeCycleRule
                            }
                        }
                    });    
                }
                
            }
            catch (AmazonS3Exception exception)
            {
                if (exception.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
                if (attemptCount++ < MaxS3BucketRetries)
                {
                    CreateS3ResourcesIfNecessary(attemptCount);
                    return;
                }
                Logger.Error(string.Format("Unable to create s3 bucket and/or lifecycle configuration rule after {0} attempts.", attemptCount), exception);
                throw;
            }
            
        }

        static ILog Logger = LogManager.GetLogger(typeof(SqsQueueCreator));
    }
}
