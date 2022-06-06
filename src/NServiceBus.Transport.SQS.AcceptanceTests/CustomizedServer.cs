namespace NServiceBus.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(bool supportsPublishSubscribe)
        {
            TransportConfiguration = new ConfigureEndpointSqsTransport(supportsPublishSubscribe);
        }
    }
}