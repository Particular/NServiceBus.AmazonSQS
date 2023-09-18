namespace NServiceBus.AcceptanceTests
{
    using System.Runtime.CompilerServices;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;

    public class TestSuiteConstraints : ITestSuiteConstraints
    {
        public bool SupportsCrossQueueTransactions => false;

        public bool SupportsDtc => false;

        public bool SupportsDelayedDelivery => true;

        public bool SupportsNativePubSub => true;

        public bool SupportsOutbox => false;

        public bool SupportsPurgeOnStartup => false;

        public IConfigureEndpointTestExecution CreateTransportConfiguration()
        {
            return new ConfigureEndpointSqsTransport();
        }

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration()
        {
            return new ConfigureEndpointAcceptanceTestingPersistence();
        }

        [ModuleInitializer]
        public static void Initialize() => ITestSuiteConstraints.Current = new TestSuiteConstraints();
    }
}
