namespace NServiceBus.Transport.SQS.Configure
{
    using System;
    using Settings;

    /// <summary>
    /// Publish-subscribe migration mode configuration.
    /// </summary>
    [ObsoleteEx(
        Message = @"The compatibility mode will be deprecated in the next major version of the transport. Switch to native publish/subscribe mode using SNS instead.",
        TreatAsErrorFromVersion = "6.0",
        RemoveInVersion = "7.0")]
    public class SqsSubscriptionMigrationModeSettings : SubscriptionMigrationModeSettings
    {
        SettingsHolder settings;

        internal SqsSubscriptionMigrationModeSettings(SettingsHolder settings) : base(settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Overrides the default value of 5 seconds for SNS topic cache.
        /// </summary>
        /// <param name="ttl">Topic cache TTL.</param>
        public SqsSubscriptionMigrationModeSettings TopicCacheTTL(TimeSpan ttl)
        {
            Guard.AgainstNegativeAndZero(nameof(ttl), ttl);

            settings.Set(SettingsKeys.NotFoundTopicsCacheTTL, ttl);

            return this;
        }

        /// <summary>
        /// Overrides the default value of 5 seconds for SNS topic subscribers cache.
        /// </summary>
        /// <param name="ttl">Subscription cache TTL.</param>
        public SubscriptionMigrationModeSettings SubscriptionsCacheTTL(TimeSpan ttl)
        {
            Guard.AgainstNegativeAndZero(nameof(ttl), ttl);

            settings.Set(SettingsKeys.SubscriptionsCacheTTL, ttl);

            return this;
        }

        /// <summary>
        /// Overrides the default value specified at the queue level for message visibility timeout.
        /// </summary>
        /// <param name="timeoutInSeconds">Message visibility timeout.</param>
        public SubscriptionMigrationModeSettings MessageVisibilityTimeout(int timeoutInSeconds)
        {
            //HINT: See https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html
            if (timeoutInSeconds < 0 || timeoutInSeconds > TimeSpan.FromHours(12).TotalSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutInSeconds));
            }

            settings.Set(SettingsKeys.MessageVisibilityTimeout, timeoutInSeconds);

            return this;
        }
    }
}
