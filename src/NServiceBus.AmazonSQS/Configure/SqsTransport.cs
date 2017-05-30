namespace NServiceBus
{
    using Routing;
    using Settings;
    using System;
    using Transport;

    public class SqsTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        public override string ExampleConnectionStringForErrorMessage
            => "";

        public override bool RequiresConnectionString => false;

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException($"{nameof(SqsTransport)} does not require a connection string, but a connection string was provided. Use the code based configuration methods instead.");
            }

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
