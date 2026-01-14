namespace NServiceBus.Transport.SQS.Configure;

static class SettingsKeys
{
    const string Prefix = "NServiceBus.AmazonSQS.";

    public const string SubscriptionsCacheTTL = Prefix + nameof(SubscriptionsCacheTTL);
    public const string NotFoundTopicsCacheTTL = Prefix + nameof(NotFoundTopicsCacheTTL);
    public const string MessageGroupIdSelector = Prefix + nameof(MessageGroupIdSelector);
}
