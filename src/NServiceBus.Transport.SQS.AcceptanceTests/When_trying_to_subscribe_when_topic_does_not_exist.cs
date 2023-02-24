namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Features;
    using NUnit.Framework;

    public class When_trying_to_subscribe_when_topic_does_not_exist : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_log_exception_with_topic_name()
        {
            var messageId = Guid.NewGuid();

            await Scenario.Define<Context>(c =>
            {
                c.MessageId = messageId;
                c.EnableInstallers = true;
                c.DisableAutoSubscribe = true;
            })
            .WithEndpoint<Endpoint>()
            .Done(context => true)
            .Run();

            var context = await Scenario.Define<Context>(c =>
            {
                c.MessageId = messageId;
                c.EnableInstallers = false;
                c.DisableAutoSubscribe = false;
            })
            .WithEndpoint<Endpoint>()
            .Done(context => true)
            .Run();

            Assert.IsTrue(context.Logs.Any(a => a.Message.Contains("not found") && a.Message.Contains("When_trying_to_subscribe_when_topic_does_not_exist-MyEvent")));
        }

        public class Context : ScenarioContext
        {
            public Guid MessageId { get; set; }
            public bool EnableInstallers { get; set; }
            public bool DisableAutoSubscribe { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServerWithNoInstallers>((e, c) =>
                {
                    var ourContext = c.ScenarioContext as Context;
                    if (ourContext.EnableInstallers)
                    {
                        e.EnableInstallers();
                    }
                    if (ourContext.DisableAutoSubscribe)
                    {
                        e.DisableFeature<AutoSubscribe>();
                    }
                });

            class Handler : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }

        }

        public class MyMessage : ICommand
        {
        }

        public class MyEvent : IEvent
        {
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


}