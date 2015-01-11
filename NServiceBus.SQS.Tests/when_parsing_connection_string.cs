using NUnit.Framework;
using System;

namespace NServiceBus.SQS.Tests
{
    [TestFixture]
    public class when_parsing_connection_string
    {
        [Test]
		public void parsing_valid_region_works()
        {
            var result = SqsConnectionStringParser.Parse("Region=ap-southeast-2;");

            Assert.AreEqual(Amazon.RegionEndpoint.APSoutheast2, result.Region);
        }

        [Test]
        public void invalid_region_throws()
        {
            Assert.Throws<ArgumentException>(() => SqsConnectionStringParser.Parse("Region=not-a-valid-region;"));
        }

		[Test]
		public void parsing_s3_bucket_works()
		{
			var result = SqsConnectionStringParser.Parse(
				"Region=ap-southeast-2;S3BucketForLargeMessages=myTestBucket;S3KeyPrefix=blah\blah;");

			Assert.AreEqual("myTestBucket", result.S3BucketForLargeMessages);
		}

		[Test]
		public void throws_if_s3_bucket_is_specified_without_key_prefix()
		{
			Assert.Throws<ArgumentException>(() => SqsConnectionStringParser.Parse(
				"Region=ap-southeast-2;S3BucketForLargeMessages=myTestBucket;"));
		}

		[Test]
		public void parsing_max_ttl_days_works()
		{
			var result = SqsConnectionStringParser.Parse(
				"Region=ap-southeast-2;S3BucketForLargeMessages=myTestBucket;S3KeyPrefix=blah\blah;MaxTTLDays=1");

			Assert.AreEqual(1, result.MaxTTLDays);
		}

        [Test]
        public void invalid_max_ttl_days_throws()
        {
            Assert.Throws<ArgumentException>(() => SqsConnectionStringParser.Parse(
                "Region=ap-southeast-2;S3BucketForLargeMessages=myTestBucket;S3KeyPrefix=blah\blah;MaxTTLDays=100"));
        }

        [Test]
        public void parsing_max_receive_message_batch_size_works()
        {
            var result = SqsConnectionStringParser.Parse(
                "Region=ap-southeast-2;MaxReceiveMessageBatchSize=1");

            Assert.AreEqual(1, result.MaxReceiveMessageBatchSize);
        }

        [Test]
        public void invalid_max_receive_message_batch_size_throws()
        {
            Assert.Throws<ArgumentException>(() => SqsConnectionStringParser.Parse(
                "Region=ap-southeast-2;S3BucketForLargeMessages=myTestBucket;MaxReceiveMessageBatchSize=100"));
        }

    }
}
