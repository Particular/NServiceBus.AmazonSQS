namespace NServiceBus.SQS
{
	using Amazon;

    class SqsConnectionConfiguration
    {
		public SqsConnectionConfiguration()
		{
			S3MaxBodyAgeDays = 5;
		}

        public RegionEndpoint Region { get; set; }

		public string S3BucketForLargeMessages { get; set; }

		public string S3KeyPrefix { get; set; }

		public int S3MaxBodyAgeDays { get; set; }
    }
}
