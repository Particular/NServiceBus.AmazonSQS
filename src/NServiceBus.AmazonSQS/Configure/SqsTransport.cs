namespace NServiceBus
{
    using System;
    using Routing;
    using Settings;
    using Transport;

    /// <summary>
    /// Sqs transport definition.
    /// </summary>
    public class SqsTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        /// <inheritdoc />
        public override string ExampleConnectionStringForErrorMessage => "";

        /// <inheritdoc />
        public override bool RequiresConnectionString => false;

        /// <inheritdoc />
        public override Transport.TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException($"{nameof(SqsTransport)} does not require a connection string, but a connection string was provided. Use the code based configuration methods instead.");
            }

            settings.SetDefault(SettingsKeys.S3BucketForLargeMessages, string.Empty);
            settings.SetDefault(SettingsKeys.S3KeyPrefix, string.Empty);
            settings.SetDefault(SettingsKeys.MaxTimeToLive, TimeSpan.FromDays(4));
            settings.SetDefault(SettingsKeys.QueueNamePrefix, string.Empty);

            return new TransportInfrastructure(settings);
        }
    }
}