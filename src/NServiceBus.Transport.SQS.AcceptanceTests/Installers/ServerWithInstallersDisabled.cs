namespace NServiceBus.AcceptanceTests.Installers
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class ServerWithInstallersDisabled : IEndpointSetupTemplate
    {
        public IConfigureEndpointTestExecution TransportConfiguration { get; set; } = TestSuiteConstraints.Current.CreateTransportConfiguration();

        public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);

            builder.Recoverability()
                .Delayed(delayed => delayed.NumberOfRetries(0))
                .Immediate(immediate => immediate.NumberOfRetries(0));
            builder.SendFailedMessagesTo("error");

            await builder.DefineTransport(TransportConfiguration, runDescriptor, endpointConfiguration).ConfigureAwait(false);

            await configurationBuilderCustomization(builder).ConfigureAwait(false);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            builder.TypesToIncludeInScan(endpointConfiguration.GetTypesScopedByTestClass());

            return builder;
        }
    }
}
