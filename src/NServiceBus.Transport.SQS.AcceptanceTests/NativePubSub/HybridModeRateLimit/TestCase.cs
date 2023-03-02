namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using System;

    public class TestCase
    {
        public TestCase(int sequence) => Sequence = sequence;

        public int NumberOfEvents { get; internal set; }
        public TimeSpan? TestExecutionTimeout { get; internal set; }
        public int MessageVisibilityTimeout { get; internal set; } = DefaultMessageVisibilityTimeout;
        public TimeSpan SubscriptionsCacheTTL { get; internal set; } = DefaultSubscriptionCacheTTL;
        public TimeSpan NotFoundTopicsCacheTTL { get; internal set; } = DefaultTopicCacheTTL;
        public bool PreDeployInfrastructure { get; internal set; } = DefaultPreDeployInfrastructure;
        public int DeployInfrastructureDelay { get; internal set; } = DefaultDeployInfrastructureDelay;
        public int Sequence { get; }

        public override string ToString() => $"#{Sequence}, " +
            $"{nameof(NumberOfEvents)}: {NumberOfEvents}, " +
            $"{nameof(MessageVisibilityTimeout)}: {(MessageVisibilityTimeout == DefaultMessageVisibilityTimeout ? "default" : MessageVisibilityTimeout.ToString())}, " +
            $"{nameof(TestExecutionTimeout)}: {TestExecutionTimeout?.ToString() ?? "default"}, " +
            $"{nameof(SubscriptionsCacheTTL)}: {(SubscriptionsCacheTTL == DefaultSubscriptionCacheTTL ? "default" : SubscriptionsCacheTTL.ToString())}, " +
            $"{nameof(NotFoundTopicsCacheTTL)}: {(NotFoundTopicsCacheTTL == DefaultTopicCacheTTL ? "default" : NotFoundTopicsCacheTTL.ToString())}";

        static TimeSpan DefaultSubscriptionCacheTTL = TimeSpan.FromSeconds(5);
        static TimeSpan DefaultTopicCacheTTL = TimeSpan.FromSeconds(5);
        static int DefaultMessageVisibilityTimeout = 30;
        static int DefaultDeployInfrastructureDelay = 65000;
        static bool DefaultPreDeployInfrastructure = true;
    }
}
