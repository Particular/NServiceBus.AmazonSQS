namespace NServiceBus.SQS
{
	using Amazon.Runtime;
	using Amazon.S3;
	using Amazon.SQS;
	using System;

    internal class AwsClientFactory : IAwsClientFactory
    {
        public IAmazonSQS CreateSqsClient(SqsConnectionConfiguration connectionConfiguration)
        {
            return new AmazonSQSClient(new EnvironmentVariablesAWSCredentials(), connectionConfiguration.Region);
        }

		public IAmazonS3 CreateS3Client(SqsConnectionConfiguration connectionConfiguration)
		{
			return new AmazonS3Client(new EnvironmentVariablesAWSCredentials(), connectionConfiguration.Region);
		}
	}
}
