namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using System;

    public class TestCase
    {
        public int NumberOfEvents { get; internal set; }
        public TimeSpan? TestExecutionTimeout { get; internal set; }
        public int? MessageVisibilityTimeout { get; internal set; }
        public TimeSpan? SubscriptionsCacheTTL { get; internal set; }

        public override string ToString() => $"{nameof(NumberOfEvents)}: {NumberOfEvents}, " +
            $"{nameof(MessageVisibilityTimeout)}: {MessageVisibilityTimeout?.ToString() ?? "default"}, " +
            $"{nameof(TestExecutionTimeout)}: {TestExecutionTimeout?.ToString() ?? "default"} " +
            $"{nameof(SubscriptionsCacheTTL)}: {SubscriptionsCacheTTL?.ToString() ?? "default"}";
    }
}
