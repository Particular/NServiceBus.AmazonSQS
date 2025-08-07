namespace NServiceBus.Transport.SQS.Configure;

using System;
using Settings;
using Particular.Obsoletes;

/// <summary>
/// Publish-subscribe migration mode configuration.
/// </summary>
[PreObsolete("https://github.com/Particular/NServiceBus/issues/6471",
    Note = "Hybrid pub/sub support cannot be obsolete until there is a viable migration path to native pub/sub",
    Message = "Hybrid pub/sub is no longer supported, use native pub/sub instead")]
public partial class SqsSubscriptionMigrationModeSettings : SubscriptionMigrationModeSettings
{
    SettingsHolder settings;

    internal SqsSubscriptionMigrationModeSettings(SettingsHolder settings) : base(settings) => this.settings = settings;

    /// <summary>
    /// Overrides the default value of 5 seconds for SNS topic cache.
    /// </summary>
    /// <param name="ttl">Topic cache TTL.</param>
    public SqsSubscriptionMigrationModeSettings TopicCacheTTL(TimeSpan ttl)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        settings.Set(SettingsKeys.NotFoundTopicsCacheTTL, ttl);

        return this;
    }

    /// <summary>
    /// Overrides the default value of 5 seconds for SNS topic subscribers cache.
    /// </summary>
    /// <param name="ttl">Subscription cache TTL.</param>
    public SubscriptionMigrationModeSettings SubscriptionsCacheTTL(TimeSpan ttl)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        settings.Set(SettingsKeys.SubscriptionsCacheTTL, ttl);

        return this;
    }
}