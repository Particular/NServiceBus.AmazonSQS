namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Configuration.AdvanceExtensibility;
    using Features;
    using NServiceBus.Config.ConfigurationSource;
    using System.Linq;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public DefaultServer()
        {
            typesToInclude = new List<Type>();
        }

        public DefaultServer(List<Type> typesToInclude)
        {
            this.typesToInclude = typesToInclude;
        }

#pragma warning disable CS0618
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, IConfigurationSource configSource, Action<EndpointConfiguration> configurationBuilderCustomization)
#pragma warning restore CS0618
        {
            var settings = runDescriptor.Settings;

            var types = endpointConfiguration.GetTypesScopedByTestClass();

            typesToInclude.AddRange(types);

            // Dodgy hack to shorten endpoint names
            var endpointName = new string(endpointConfiguration.EndpointName.Reverse().Take(40).Reverse().ToArray());

            var configuration = new EndpointConfiguration(endpointName);

            configuration.TypesToIncludeInScan(typesToInclude);
            configuration.CustomConfigurationSource(configSource);
            configuration.EnableInstallers();

            configuration.DisableFeature<TimeoutManager>();

            var recoverability = configuration.Recoverability();
            recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
            recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
            configuration.SendFailedMessagesTo("error");

            await configuration.DefineTransport(settings, endpointConfiguration).ConfigureAwait(false);

            configuration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            await configuration.DefinePersistence(settings, endpointConfiguration).ConfigureAwait(false);

            configuration.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);
            configurationBuilderCustomization(configuration);

            return configuration;
        }

        List<Type> typesToInclude;
    }
}