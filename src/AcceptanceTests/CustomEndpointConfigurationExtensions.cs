namespace NServiceBus.AcceptanceTests
{
    using Configuration.AdvancedExtensibility;
    using Transport.SQS.Configure;

    public static class CustomEndpointConfigurationExtensions
    {
        public static TransportExtensions<SqsTransport> ConfigureSqsTransport(this EndpointConfiguration configuration)
        {
            return new TransportExtensions<SqsTransport>(configuration.GetSettings());
        }
    }
}
