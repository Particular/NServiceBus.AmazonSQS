namespace NServiceBus.SQS
{
	using Amazon;

    class SqsConnectionConfiguration
    {
		public SqsConnectionConfiguration()
		{
            MaxTTLDays = 4;
            MaxReceiveMessageBatchSize = 10;
			TruncateLongQueueNames = false;
			CredentialSource = SqsCredentialSource.EnvironmentVariables;
		}

        public RegionEndpoint Region { get; set; }

        public int MaxTTLDays { get; set; }

		public string S3BucketForLargeMessages { get; set; }

		public string S3KeyPrefix { get; set; }

        public int MaxReceiveMessageBatchSize { get; set; }

		public string QueueNamePrefix { get; set; }

		public SqsCredentialSource CredentialSource { get; set; }

		public bool TruncateLongQueueNames { get; set; }
    }
}
