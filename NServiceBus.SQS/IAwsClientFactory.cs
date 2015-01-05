namespace NServiceBus.SQS
{
	using Amazon.S3;
	using Amazon.SQS;

	internal interface IAwsClientFactory
	{
		IAmazonSQS CreateSqsClient(SqsConnectionConfiguration connectionConfiguration);

		IAmazonS3 CreateS3Client(SqsConnectionConfiguration connectionConfiguration);
	}
}
