namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using NUnit.Framework;
    using Routing;
    using Routing.NativePublishSubscribe;
    using Sagas;
    using Versioning;
    using MessageDriven = Routing.MessageDrivenSubscriptions;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            PreventInconclusiveTestsFromRunning(endpointName);

            var transportConfig = configuration.UseTransport<SqsTransport>();

            transportConfig.ConfigureSqsTransport(SetupFixture.NamePrefix);

            ApplyMappingsToSupportMultipleInheritance(endpointName, transportConfig);

            if (settings.TestExecutionTimeout == null)
            {
                //If it's not null it means it has been set to a custom
                //value in the test. We don't want to overwrite that
                settings.TestExecutionTimeout = TimeSpan.FromSeconds(120);
            }

            return Task.FromResult(0);
        }

        static void ApplyMappingsToSupportMultipleInheritance(string endpointName, TransportExtensions<SqsTransport> transportConfig)
        {
            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_multi_subscribing_to_a_polymorphic_event.Subscriber)))
            {
                transportConfig.MapEvent<When_multi_subscribing_to_a_polymorphic_event.IMyEvent, When_multi_subscribing_to_a_polymorphic_event.MyEvent1>();
                transportConfig.MapEvent<When_multi_subscribing_to_a_polymorphic_event.IMyEvent, When_multi_subscribing_to_a_polymorphic_event.MyEvent2>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_subscribing_to_a_base_event.GeneralSubscriber)))
            {
                transportConfig.MapEvent<When_subscribing_to_a_base_event.IBaseEvent, When_subscribing_to_a_base_event.SpecificEvent>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_publishing_an_event_implementing_two_unrelated_interfaces.Subscriber)))
            {
                transportConfig.MapEvent<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA, When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent>();
                transportConfig.MapEvent<When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB, When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_started_by_base_event_from_other_saga.SagaThatIsStartedByABaseEvent)))
            {
                transportConfig.MapEvent<When_started_by_base_event_from_other_saga.IBaseEvent, When_started_by_base_event_from_other_saga.ISomethingHappenedEvent>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_multiple_versions_of_a_message_is_published.V1Subscriber)))
            {
                transportConfig.MapEvent<When_multiple_versions_of_a_message_is_published.V1Event, When_multiple_versions_of_a_message_is_published.V2Event>();
            }
        }

        public Task Cleanup()
        {
            // Queues are cleaned up once, globally, in SetupFixture.
            return Task.FromResult(0);
        }

        static void PreventInconclusiveTestsFromRunning(string endpointName)
        {
            if (endpointName == Conventions.EndpointNamingConvention(typeof(MessageDriven.When_publishing_from_sendonly.SendOnlyPublisher)))
            {
                Assert.Inconclusive("Test is not using endpoint naming conventions in hardcoded subscription storage. Should be fixed in core vNext.");
            }
        }
    }
}