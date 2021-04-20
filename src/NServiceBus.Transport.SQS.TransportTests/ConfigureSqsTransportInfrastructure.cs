using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using NServiceBus;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using TransportTests;

public class ConfigureSqsTransportInfrastructure : IConfigureTransportInfrastructure
{
    const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";
    public const string S3Prefix = "test";
    public static string S3BucketName;

    public TransportDefinition CreateTransportDefinition()
    {
        var transport = new SqsTransport(CreateSqsClient(), CreateSnsClient())
        {
            QueueNamePrefix = SetupFixture.GetNamePrefix(),
            TopicNamePrefix = SetupFixture.GetNamePrefix(),
            QueueNameGenerator = TestNameHelper.GetSqsQueueName,
            TopicNameGenerator = TestNameHelper.GetSnsTopicName
        };

        S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

        if (!string.IsNullOrEmpty(S3BucketName))
        {
            transport.S3 = new S3Settings(S3BucketName, S3Prefix, CreateS3Client());
        }

        return transport;
    }

    public Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, string inputQueueName,
        string errorQueueName, CancellationToken cancellationToken)
    {
        return transportDefinition.Initialize(hostSettings, new[]
        {
            new ReceiveSettings(inputQueueName, inputQueueName, true, false, errorQueueName),
        }, new[]
        {
            errorQueueName
        }, cancellationToken);
    }

    public static IAmazonSQS CreateSqsClient()
    {
        var credentials = new EnvironmentVariablesAWSCredentials();
        return new AmazonSQSClient(credentials);
    }

    public static IAmazonSimpleNotificationService CreateSnsClient()
    {
        var credentials = new EnvironmentVariablesAWSCredentials();
        return new AmazonSimpleNotificationServiceClient(credentials);
    }

    public static IAmazonS3 CreateS3Client()
    {
        var credentials = new EnvironmentVariablesAWSCredentials();
        return new AmazonS3Client(credentials);
    }

    public Task Cleanup(CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }
}