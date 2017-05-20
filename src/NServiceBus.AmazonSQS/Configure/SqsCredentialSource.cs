namespace NServiceBus
{
	public enum SqsCredentialSource
	{
        /// <summary>
        /// The endpoint will extract an AWS Access Key ID and AWS Secret Access Key from the environment variables AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY respectively.
        /// </summary>
		EnvironmentVariables,
        /// <summary>
        /// The endpoint will use the credentials of the first EC2 role attached to the EC2 instance. Naturally this only valid when running the endpoint on an EC2 instance.
        /// </summary>
		InstanceProfile
    }
}
