namespace NServiceBus
{
    using System;
    using Features;
    using Routing;
    using Settings;
    using Transport;
    using Transport.SQS;
    using Transport.SQS.Configure;
    using TransportInfrastructure = Transport.TransportInfrastructure;

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
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException($"{nameof(SqsTransport)} does not require a connection string, but a connection string was provided. Use the code based configuration methods instead.");
            }

            settings.SetDefault(SettingsKeys.S3BucketForLargeMessages, string.Empty);
            settings.SetDefault(SettingsKeys.S3KeyPrefix, string.Empty);
            settings.SetDefault(SettingsKeys.MaxTimeToLive, TimeSpan.FromDays(4));
            settings.SetDefault(SettingsKeys.QueueNamePrefix, string.Empty);
            settings.SetDefault(SettingsKeys.TopicNamePrefix, string.Empty);
            settings.SetDefault(SettingsKeys.FullTopicNameForPolicies, true);
            settings.SetDefault(SettingsKeys.ForceSettlementForPolicies, false);
            settings.SetDefault(SettingsKeys.AssumePolicyHasAppropriatePermissions, false);

            // needed to only enable the feature when the transport is used
            settings.Set(typeof(SettlePolicy).FullName, FeatureState.Enabled);

            return new SqsTransportInfrastructure(settings);
        }
    }
}