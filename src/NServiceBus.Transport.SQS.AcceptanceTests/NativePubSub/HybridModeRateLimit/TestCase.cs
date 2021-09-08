namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using System;

    public class TestCase
    {
        public TestCase(int sequence) => Sequence = sequence;

        public int NumberOfEvents { get; internal set; }
        public TimeSpan? TestExecutionTimeout { get; internal set; }
        public int? MessageVisibilityTimeout { get; internal set; }
        public TimeSpan? SubscriptionsCacheTTL { get; internal set; }
        public TimeSpan? NotFoundTopicsCacheTTL { get; internal set; }
        public int Sequence { get; }

        public override string ToString() => $"#{Sequence}, " +
            $"{nameof(NumberOfEvents)}: {NumberOfEvents}, " +
            $"{nameof(MessageVisibilityTimeout)}: {MessageVisibilityTimeout?.ToString() ?? "default"}, " +
            $"{nameof(TestExecutionTimeout)}: {TestExecutionTimeout?.ToString() ?? "default"}, " +
            $"{nameof(SubscriptionsCacheTTL)}: {SubscriptionsCacheTTL?.ToString() ?? "default"}, " +
            $"{nameof(NotFoundTopicsCacheTTL)}: {NotFoundTopicsCacheTTL?.ToString() ?? "default"}";
    }
}
