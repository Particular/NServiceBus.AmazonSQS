﻿namespace NServiceBus.AcceptanceTests.Installers
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class DefaultServerWithNoInstallers : ServerWithInstallersDisabled
    {
        public IConfigureEndpointTestExecution PersistenceConfiguration { get; set; } = TestSuiteConstraints.Current.CreatePersistenceConfiguration();

        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            base.GetConfiguration(runDescriptor, endpointConfiguration, async configuration =>
            {
                await configuration.DefinePersistence(PersistenceConfiguration, runDescriptor, endpointConfiguration);

                await configurationBuilderCustomization(configuration);
            });
    }
}
