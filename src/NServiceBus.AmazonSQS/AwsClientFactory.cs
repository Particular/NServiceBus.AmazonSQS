namespace NServiceBus.AmazonSQS
{
	using System;
	using Amazon.Runtime;
	using Amazon.S3;
	using Amazon.SQS;

    internal static class AwsClientFactory
    {
	    static AWSCredentials CreateCredentials(SqsConnectionConfiguration connectionConfiguration)
	    {
		    switch (connectionConfiguration.CredentialSource)
		    {
			    case SqsCredentialSource.EnvironmentVariables:
					return new EnvironmentVariablesAWSCredentials();
				case SqsCredentialSource.InstanceProfile:
					return new InstanceProfileAWSCredentials();
		    }
		    throw new NotImplementedException(String.Format("No implementation for credential type {0}", connectionConfiguration.CredentialSource));
	    }

	    public static IAmazonSQS CreateSqsClient(SqsConnectionConfiguration connectionConfiguration)
        {
			return new AmazonSQSClient(CreateCredentials(connectionConfiguration), connectionConfiguration.Region);
        }

		public static IAmazonS3 CreateS3Client(SqsConnectionConfiguration connectionConfiguration)
		{
			return new AmazonS3Client(CreateCredentials(connectionConfiguration), connectionConfiguration.Region);
		}
	}
}
