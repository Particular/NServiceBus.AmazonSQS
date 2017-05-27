namespace NServiceBus
{
    using Routing;
    using Settings;
    using Transport;
    using Transports.SQS;

    public class SqsTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        public override string ExampleConnectionStringForErrorMessage
            => "";

        public override bool RequiresConnectionString => false;

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            settings.SetDefault(SqsTransportSettings.Keys.S3BucketForLargeMessages, string.Empty);
            settings.SetDefault(SqsTransportSettings.Keys.S3KeyPrefix, string.Empty);
            settings.SetDefault(SqsTransportSettings.Keys.MaxTTLDays, 4);
            settings.SetDefault(SqsTransportSettings.Keys.CredentialSource, SqsCredentialSource.EnvironmentVariables);
            settings.SetDefault(SqsTransportSettings.Keys.ProxyHost, string.Empty);
            settings.SetDefault(SqsTransportSettings.Keys.ProxyPort, 0);
            settings.SetDefault(SqsTransportSettings.Keys.QueueNamePrefix, string.Empty);

            return new SqsTransportInfrastructure(settings, connectionString);
        }
    }
}
