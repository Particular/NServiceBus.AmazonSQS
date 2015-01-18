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

		public IAwsClientFactory ClientFactory { get; set; }

        public void CreateQueueIfNecessary(Address address, string account)
        {
			using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
            {
                var sqsRequest = new CreateQueueRequest
                {
	                QueueName = address.ToSqsQueueName(ConnectionConfiguration),
                };
	            var createQueueResponse = sqs.CreateQueue(sqsRequest);

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

	            sqs.SetQueueAttributes(sqsAttributesRequest);
            }

            if (!string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
            {
                using (var s3 = ClientFactory.CreateS3Client(ConnectionConfiguration))
                {
                    // determine if the configured bucket exists; create it if it doesn't
                    var listBucketsResponse = s3.ListBuckets(new ListBucketsRequest());
                    var bucketExists = listBucketsResponse.Buckets.Any(x => x.BucketName.ToLower() == ConnectionConfiguration.S3BucketForLargeMessages.ToLower());
                    if (!bucketExists)
                    {
                        s3.PutBucket(new PutBucketRequest
                            {
                                BucketName = ConnectionConfiguration.S3BucketForLargeMessages
                            });
                    }

                    s3.PutLifecycleConfiguration(new PutLifecycleConfigurationRequest
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
}
