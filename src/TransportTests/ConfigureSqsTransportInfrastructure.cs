using System;
using System.Globalization;
using System.Reflection;
using NServiceBus.TransportTests;
using System.Threading.Tasks;
using NServiceBus.Settings;
using NServiceBus;
using NServiceBus.AmazonSQS.AcceptanceTests;
using NServiceBus.Unicast.Messages;

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

        bool IsMessageType(Type t) => true;
        var messageMetadataRegistry = (MessageMetadataRegistry)Activator.CreateInstance(
            type:typeof(MessageMetadataRegistry),
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null, 
            args: new object[] {(Func<Type, bool>)IsMessageType},
            culture: CultureInfo.InvariantCulture);
        settings.Set(messageMetadataRegistry);

        return new TransportConfigurationResult
        {
            TransportInfrastructure = sqsTransport.Initialize(settings, ""),
            PurgeInputQueueOnStartup = false
        };
    }
}
