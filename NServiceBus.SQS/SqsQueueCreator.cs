namespace NServiceBus.Transports.SQS
{
	using Amazon.S3;
	using Amazon.S3.Model;
	using Amazon.SQS.Model;
	using NServiceBus.SQS;
	using System.Collections.Generic;

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
	                QueueName = address.ToSqsQueueName()
                };
	            sqs.CreateQueue(sqsRequest);
            }

			using (var s3 = ClientFactory.CreateS3Client(ConnectionConfiguration))
			{
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
									Days = ConnectionConfiguration.S3MaxBodyAgeDays
								}
							}
						}
					}
				});
			}
        }
    }
}
