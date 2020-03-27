using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AmazonSQS.AcceptanceTests;
using NServiceBus.AmazonSQS.Tests;
using NServiceBus.Settings;
using NServiceBus.TransportTests;

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

        sqsConfig.ConfigureSqsTransport(SetupFixture.NamePrefix);

        settings.SetupMessageMetadataRegistry();

        return new TransportConfigurationResult
        {
            TransportInfrastructure = sqsTransport.Initialize(settings, ""),
            PurgeInputQueueOnStartup = false
        };
    }
}