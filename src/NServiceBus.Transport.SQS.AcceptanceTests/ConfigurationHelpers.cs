namespace NServiceBus.AcceptanceTests
{
    using EndpointTemplates;

    static class ConfigurationHelpers
    {
        public static SqsTransport ConfigureSqsTransport(this EndpointConfiguration configuration) => (SqsTransport)configuration.ConfigureTransport();
    }
}