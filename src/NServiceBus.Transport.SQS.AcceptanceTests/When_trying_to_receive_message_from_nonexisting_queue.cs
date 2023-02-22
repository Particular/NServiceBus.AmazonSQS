namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;

    public class When_trying_to_receive_message_from_nonexisting_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw_exception_with_queue_name()
        {
            var messageId = Guid.NewGuid();

            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            {
                await Scenario.Define<Context>(c =>
                    {
                        c.MessageId = messageId;
                    })
                    .WithEndpoint<Endpoint>()
                    .Done(context => true)
                    .Run();
            });

            Assert.IsTrue(exception.Message.Contains("TryingToReceiveMessageFromNonexistingQueue"));
        }

        public class Context : ScenarioContext
        {
            public Guid MessageId { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServerWithNoInstallers>();
        }

        public class MyMessage : ICommand
        {
        }
    }

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