using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    }
}
