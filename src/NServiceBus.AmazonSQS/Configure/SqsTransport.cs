namespace NServiceBus
{
    using Routing;
    using Settings;
    using System;
    using Transport;

    /// <summary>
    /// Sqs transport definition.
    /// </summary>
    public class SqsTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        /// <summary>
        /// Connection string error message.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage
            => "";

        /// <summary>
        /// Requires connection string.
        /// </summary>
        public override bool RequiresConnectionString => false;

        /// <summary>
        /// Initializes the transport definition.
        /// </summary>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException($"{nameof(SqsTransport)} does not require a connection string, but a connection string was provided. Use the code based configuration methods instead.");
            }

            settings.SetDefault(SqsTransportSettingsKeys.S3BucketForLargeMessages, string.Empty);
            settings.SetDefault(SqsTransportSettingsKeys.S3KeyPrefix, string.Empty);
            settings.SetDefault(SqsTransportSettingsKeys.MaxTTLDays, 4);
            settings.SetDefault(SqsTransportSettingsKeys.CredentialSource, SqsCredentialSource.EnvironmentVariables);
            settings.SetDefault(SqsTransportSettingsKeys.ProxyHost, string.Empty);
            settings.SetDefault(SqsTransportSettingsKeys.ProxyPort, 0);
            settings.SetDefault(SqsTransportSettingsKeys.QueueNamePrefix, string.Empty);

            return new SqsTransportInfrastructure(settings);
        }
    }
}
