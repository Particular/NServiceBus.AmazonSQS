using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.Transport;
using NServiceBus.Transport.SQS.Tests;
using NServiceBus.TransportTests;
using TransportTests;

public class ConfigureSqsTransportInfrastructure : IConfigureTransportInfrastructure
{
    const string S3BucketEnvironmentVariableName = "NSERVICEBUS_AMAZONSQS_S3BUCKET";
    public const string S3Prefix = "test";
    public static string S3BucketName;

    public TransportDefinition CreateTransportDefinition()
    {
        var transport = new SqsTransport(ClientFactories.CreateSqsClient(), ClientFactories.CreateSnsClient())
        {
            QueueNamePrefix = SetupFixture.GetNamePrefix(),
            TopicNamePrefix = SetupFixture.GetNamePrefix(),
            QueueNameGenerator = TestNameHelper.GetSqsQueueName,
            TopicNameGenerator = TestNameHelper.GetSnsTopicName
        };

        S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

        if (!string.IsNullOrEmpty(S3BucketName))
        {
            transport.S3 = new S3Settings(S3BucketName, S3Prefix, ClientFactories.CreateS3Client());
        }

        return transport;
    }

    public Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress inputQueueName,
        string errorQueueName, CancellationToken cancellationToken) =>
        transportDefinition.Initialize(hostSettings, new[]
        {
            new ReceiveSettings(inputQueueName.ToString(), inputQueueName, true, false, errorQueueName),
        }, new[]
        {
            errorQueueName
        }, cancellationToken);

    public Task Cleanup(CancellationToken cancellationToken) => Task.CompletedTask;
}