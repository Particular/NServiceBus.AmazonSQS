namespace NServiceBus.AmazonSQS
{
    using System;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SQS;
    using System.Net;

    internal static class AwsClientFactory
    {
        static string NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_USERNAME = nameof(NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_USERNAME);
        static string NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_PASSWORD = nameof(NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_PASSWORD);

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

        static void SetProxyConfig(ClientConfig clientConfig, SqsConnectionConfiguration connectionConfig)
        {
            if (!string.IsNullOrEmpty(connectionConfig.ProxyHost))
            {
                var userName = Environment.GetEnvironmentVariable(NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_USERNAME);
                var password = Environment.GetEnvironmentVariable(NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_PASSWORD);

                clientConfig.ProxyCredentials = new NetworkCredential(userName, password);
                clientConfig.ProxyHost = connectionConfig.ProxyHost;
                clientConfig.ProxyPort = connectionConfig.ProxyPort;
            }
        }

	    public static IAmazonSQS CreateSqsClient(SqsConnectionConfiguration connectionConfiguration)
	    {
	        var config = new AmazonSQSConfig
	        {
	            RegionEndpoint = connectionConfiguration.Region
	        };

            SetProxyConfig(config, connectionConfiguration);

			var result = new AmazonSQSClient(CreateCredentials(connectionConfiguration), config);
            return result;
        }

		public static IAmazonS3 CreateS3Client(SqsConnectionConfiguration connectionConfiguration)
		{
		    var config = new AmazonS3Config
		    {
		        RegionEndpoint = connectionConfiguration.Region,
		    };

            SetProxyConfig(config, connectionConfiguration);

            var result = new AmazonS3Client(CreateCredentials(connectionConfiguration), config);
            return result;
		}
	}
}
