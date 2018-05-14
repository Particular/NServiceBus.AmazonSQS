using NServiceBus.TransportTests;
using System.Threading.Tasks;
using NServiceBus.Settings;
using NServiceBus;
using NServiceBus.AmazonSQS.AcceptanceTests;

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
        
        sqsConfig.ConfigureSqsTransport(SetupFixture.SqsQueueNamePrefix);

        return new TransportConfigurationResult
        {
            TransportInfrastructure = sqsTransport.Initialize(settings, ""),
            PurgeInputQueueOnStartup = false
        };
    }
}
