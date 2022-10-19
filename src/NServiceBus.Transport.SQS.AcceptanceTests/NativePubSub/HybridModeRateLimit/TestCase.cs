namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;

    public class TestCase
    {
        public TestCase(int sequence) => Sequence = sequence;

        public int NumberOfEvents { get; internal set; }
        public TimeSpan? TestExecutionTimeout { get; internal set; }
        public int MessageVisibilityTimeout { get; internal set; } = DefaultMessageVisibilityTimeout;
        public TimeSpan SubscriptionsCacheTTL { get; internal set; } = DefaultSubscriptionCacheTTL;
        public TimeSpan NotFoundTopicsCacheTTL { get; internal set; } = DefaultTopicCacheTTL;
        public bool PreDeployInfrastructure { get; internal set; } = true;
        public int DeployInfrastructureDelay { get; internal set; } = 60000;
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

        public readonly Func<Type, string> customConvention = t =>
        {
            var classAndEndpoint = t.FullName.Split('.').Last();
            var testName = classAndEndpoint.Split('+').First();
            testName = testName.Replace("When_", "");
            var endpointBuilder = classAndEndpoint.Split('+').Last();
            testName = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);
            testName = testName.Replace("_", "");
            var instanceGuid = Regex.Replace(Convert.ToBase64String(t.GUID.ToByteArray()), "[/+=]", "").ToUpperInvariant();
            return testName + "." + instanceGuid + "." + endpointBuilder;
        };

    }
}
