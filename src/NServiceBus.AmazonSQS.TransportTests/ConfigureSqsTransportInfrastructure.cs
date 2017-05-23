using NServiceBus.TransportTests;
using System.Threading.Tasks;
using NServiceBus.Settings;
using NServiceBus;

public class ConfigureSqsTransportInfrastructure : IConfigureTransportInfrastructure
{
    public Task Cleanup()
    {
        return Task.FromResult(0);
    }

    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transactionMode)
    {
        var sqsTransport = new SqsTransport();
        var sqsConfig = new TransportExtensions<SqsTransport>(settings);
        sqsConfig.Region("ap-southeast-2")
            .QueueNamePrefix("TransportTest-")
            .S3BucketForLargeMessages("sqstransportmessages1337", "test");

        return new TransportConfigurationResult
        {
            TransportInfrastructure = sqsTransport.Initialize(settings, ""),
            PurgeInputQueueOnStartup = true
        };
    }
}
