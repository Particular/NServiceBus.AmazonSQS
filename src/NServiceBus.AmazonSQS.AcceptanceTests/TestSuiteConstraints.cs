using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.AcceptanceTests
{
    public partial class TestSuiteConstraints
    {
        public IConfigureEndpointTestExecution PersistenceConfiguration => new ConfigureEndpointInMemoryPersistence();

        public bool SupportsCrossQueueTransactions => false;

        public bool SupportsDtc => false;

        public bool SupportsNativeDeferral => true;

        public bool SupportsNativePubSub => false;

        public bool SupportsOutbox => false;

        public IConfigureEndpointTestExecution TransportConfiguration
            => new ConfigureEndpointSqsTransport();
           //   => new ConfigureEndpointMsmqTransport();

    }
}
