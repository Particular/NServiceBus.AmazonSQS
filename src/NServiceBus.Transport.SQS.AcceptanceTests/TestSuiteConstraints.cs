namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsCrossQueueTransactions => false;

        public bool SupportsDtc => false;

        public bool SupportsNativeDeferral => true;

        public bool SupportsNativePubSub => true;

        public bool SupportsOutbox => false;

        public IConfigureEndpointTestExecution CreateTransportConfiguration()
        {
            return new ConfigureEndpointSqsTransport();
        }

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration()
        {
            return new ConfigureEndpointInMemoryPersistence();
        }
    }
}
