using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.Transport.SQS.Configure;
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
        string errorQueueName, CancellationToken cancellationToken)
    {
        var coreSettings = new SettingsHolder();
        coreSettings.Set(SettingsKeys.MessageVisibilityTimeout, 2);
        var settings = new HostSettings(hostSettings.Name, hostSettings.HostDisplayName, hostSettings.StartupDiagnostic,
            hostSettings.CriticalErrorAction, hostSettings.SetupInfrastructure, coreSettings);
        return transportDefinition.Initialize(settings, [
                new ReceiveSettings(inputQueueName.ToString(), inputQueueName, false, false, errorQueueName)
            ],
            [
                errorQueueName
            ], cancellationToken);
    }


    public Task Cleanup(CancellationToken cancellationToken) => Task.CompletedTask;
}