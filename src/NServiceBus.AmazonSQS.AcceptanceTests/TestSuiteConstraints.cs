using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.AcceptanceTests
{
    public partial class TestSuiteConstraints
    {
        public IConfigureEndpointTestExecution PersistenceConfiguration
        {
            get
            {
                return new ConfigureEndpointInMemoryPersistence();
            }
        }

        public bool SupportsCrossQueueTransactions
        {
            get
            {
                return false;
            }
        }

        public bool SupportsDtc
        {
            get
            {
                return false;
            }
        }

        public bool SupportsNativeDeferral
        {
            get
            {
                return true;
            }
        }

        public bool SupportsNativePubSub
        {
            get
            {
                return false;
            }
        }

        public bool SupportsOutbox
        {
            get
            {
                return false;
            }
        }

        public IConfigureEndpointTestExecution TransportConfiguration
            => new ConfigureEndpointSqsTransport();
                //return new ConfigureEndpointMsmqTransport();

    }
}
