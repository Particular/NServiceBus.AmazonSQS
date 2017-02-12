namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Amazon.AWSSupport.Model;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Moq;
    using NServiceBus.Transports.SQS;
    using NUnit.Framework;

    [TestFixture]
    public class when_creating_queues
    {

        private Mock<IAmazonS3> GetMockS3Client(string bucketName, int conflictAttempts = 1)
        {
            var mockS3Client = new Mock<IAmazonS3>();
            mockS3Client.Setup(s3 => s3.ListBuckets()).Returns(new ListBucketsResponse
            {
                Buckets = new List<S3Bucket> { new S3Bucket() { BucketName = bucketName, CreationDate = DateTime.Now } }
            });
            mockS3Client.Setup(s3 => s3.PutBucket(It.Is<PutBucketRequest>(r => r.BucketName == bucketName)));
            mockS3Client.Setup(s3 => s3.GetLifecycleConfiguration(It.Is<GetLifecycleConfigurationRequest>(r => r.BucketName == bucketName))).Returns(new GetLifecycleConfigurationResponse
            {
                Configuration = new LifecycleConfiguration()
            });

            var attemptCount = 0;

            mockS3Client.Setup(s3 => s3.PutLifecycleConfiguration(It.IsAny<PutLifecycleConfigurationRequest>())).Callback(() =>
            {
                if (attemptCount >= conflictAttempts)
                {
                    return;
                }
                attemptCount++;
                throw new AmazonS3Exception("A conflicting conditional operation is currently in progress against this resource. Please try again.", ErrorType.Sender, "409", Guid.NewGuid().ToString(), HttpStatusCode.Conflict);
            });
            return mockS3Client;
        }

        private Mock<IAmazonSQS> GetMockSqsClient()
        {
            var mockSqsClient = new Mock<IAmazonSQS>();
            mockSqsClient.Setup(sqs => sqs.CreateQueue(It.IsAny<CreateQueueRequest>())).Returns(new CreateQueueResponse
            {
                HttpStatusCode = HttpStatusCode.Created,
                QueueUrl = "https://" + Guid.NewGuid()
            });
            mockSqsClient.Setup(sqs => sqs.SetQueueAttributes(It.IsAny<SetQueueAttributesRequest>()));
            return mockSqsClient;
        }

        private SqsQueueCreator GetQueueCreator(IAmazonS3 s3Client, IAmazonSQS sqsClient, string bucketName)
        {
            return new SqsQueueCreator
            {
                ConnectionConfiguration = new SqsConnectionConfiguration
                {
                    Region = Amazon.RegionEndpoint.APSoutheast2,
                    S3BucketForLargeMessages = bucketName
                },
                S3Client = s3Client,
                SqsClient = sqsClient
            };
        }

        [Test]
        public void succeeds_when_conflict_on_first_attempt()
        {

            // Arrange
            var bucketName = Guid.NewGuid().ToString();
            var address = new Address(Guid.NewGuid().ToString(), null);

            var mockS3Client = GetMockS3Client(bucketName);
            var mockSqsClient = GetMockSqsClient();

            var sut = GetQueueCreator(mockS3Client.Object, mockSqsClient.Object, bucketName);


            // Act
            sut.CreateQueueIfNecessary(address, null);

            // Assert
            Assert.Pass();

        }

        [Test]
        public void throws_when_conflict_on_max_retry_attempts()
        {
            // Arrange
            var bucketName = Guid.NewGuid().ToString();
            var address = new Address(Guid.NewGuid().ToString(), null);

            var mockS3Client = GetMockS3Client(bucketName, SqsQueueCreator.MaxS3BucketRetries+1);
            var mockSqsClient = GetMockSqsClient();

            var sut = GetQueueCreator(mockS3Client.Object, mockSqsClient.Object, bucketName);

            // Act && Assert
            Assert.Throws<AmazonS3Exception>(() => sut.CreateQueueIfNecessary(address, null));

            
        }
    }
}