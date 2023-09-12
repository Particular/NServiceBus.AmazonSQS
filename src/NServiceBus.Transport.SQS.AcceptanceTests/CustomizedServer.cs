namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.EndpointTemplates;

    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(bool supportsPublishSubscribe)
        {
            TransportConfiguration = new ConfigureEndpointSqsTransport(supportsPublishSubscribe);
        }
    }
}