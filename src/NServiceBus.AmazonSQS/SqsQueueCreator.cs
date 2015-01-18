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

    class SqsQueueCreator : ICreateQueues
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public IAmazonSQS SqsClient { get; set; }
		
        public void CreateQueueIfNecessary(Address address, string account)
        {
            var sqsRequest = new CreateQueueRequest
            {
	            QueueName = address.ToSqsQueueName(ConnectionConfiguration),
            };
	        var createQueueResponse = SqsClient.CreateQueue(sqsRequest);

			// Set the queue attributes in a separate call. 
			// If you call CreateQueue with a queue name that already exists, and with a different
			// value for MessageRetentionPeriod, the service throws. This will happen if you 
			// change the MaxTTLDays configuration property. 
	        var sqsAttributesRequest = new SetQueueAttributesRequest
	        {
				QueueUrl = createQueueResponse.QueueUrl
	        };
			sqsAttributesRequest.Attributes.Add( QueueAttributeName.MessageRetentionPeriod,
                ((int)(TimeSpan.FromDays(ConnectionConfiguration.MaxTTLDays).TotalSeconds)).ToString());

	        SqsClient.SetQueueAttributes(sqsAttributesRequest);

            if (!string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
            {
                // determine if the configured bucket exists; create it if it doesn't
                var listBucketsResponse = S3Client.ListBuckets(new ListBucketsRequest());
                var bucketExists = listBucketsResponse.Buckets.Any(x => x.BucketName.ToLower() == ConnectionConfiguration.S3BucketForLargeMessages.ToLower());
                if (!bucketExists)
                {
                    S3Client.PutBucket(new PutBucketRequest
                        {
                            BucketName = ConnectionConfiguration.S3BucketForLargeMessages
                        });
                }

                S3Client.PutLifecycleConfiguration(new PutLifecycleConfigurationRequest
                {
                    BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                    Configuration = new LifecycleConfiguration
                    {
                        Rules = new List<LifecycleRule>
						{
							new LifecycleRule
							{
								Id = "NServiceBus.SQS.DeleteMessageBodies",
								Prefix = ConnectionConfiguration.S3KeyPrefix,
								Status = LifecycleRuleStatus.Enabled,
								Expiration = new LifecycleRuleExpiration 
								{ 
									Days = ConnectionConfiguration.MaxTTLDays
								}
							}
						}
                    }
                });
            }
        }
    }
}
