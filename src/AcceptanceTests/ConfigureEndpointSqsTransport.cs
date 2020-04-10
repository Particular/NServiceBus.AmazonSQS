namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using Conventions = AcceptanceTesting.Customization.Conventions;
    using AmazonSQS.AcceptanceTests;
    using NUnit.Framework;
    using MessageDriven = Routing.MessageDrivenSubscriptions;
    using NativePublishSubscribe = Routing.NativePublishSubscribe;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            PreventInconclusiveTestsFromRunning(endpointName);

            var transportConfig = configuration.UseTransport<SqsTransport>();

            transportConfig.ConfigureSqsTransport(SetupFixture.NamePrefix);

            ApplyMappingsToSupportMultipleInheritance(endpointName, transportConfig);

            settings.TestExecutionTimeout = TimeSpan.FromSeconds(80);

            configuration.Pipeline.Register(new ApplyPolicyValidationBehavior(), "Adds a policy validation instruction to the extension bag.");
            configuration.Pipeline.Register(new RetryIfNeededBehavior(), "Retries several times in case an DestinationNotYetReachable exception is raised");

            return Task.FromResult(0);
        }

        static void ApplyMappingsToSupportMultipleInheritance(string endpointName, TransportExtensions<SqsTransport> transportConfig)
        {
            if (endpointName == Conventions.EndpointNamingConvention(typeof(NativePublishSubscribe.When_multi_subscribing_to_a_polymorphic_event.Subscriber)))
            {
                transportConfig.MapEvent<NativePublishSubscribe.When_multi_subscribing_to_a_polymorphic_event.IMyEvent, NativePublishSubscribe.When_multi_subscribing_to_a_polymorphic_event.MyEvent1>();
                transportConfig.MapEvent<NativePublishSubscribe.When_multi_subscribing_to_a_polymorphic_event.IMyEvent, NativePublishSubscribe.When_multi_subscribing_to_a_polymorphic_event.MyEvent2>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(NativePublishSubscribe.When_subscribing_to_a_base_event.GeneralSubscriber)))
            {
                transportConfig.MapEvent<NativePublishSubscribe.When_subscribing_to_a_base_event.IBaseEvent, NativePublishSubscribe.When_subscribing_to_a_base_event.SpecificEvent>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(Routing.When_publishing_an_event_implementing_two_unrelated_interfaces.Subscriber)))
            {
                transportConfig.MapEvent<Routing.When_publishing_an_event_implementing_two_unrelated_interfaces.IEventA, Routing.When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent>();
                transportConfig.MapEvent<Routing.When_publishing_an_event_implementing_two_unrelated_interfaces.IEventB, Routing.When_publishing_an_event_implementing_two_unrelated_interfaces.CompositeEvent>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(Sagas.When_started_by_base_event_from_other_saga.SagaThatIsStartedByABaseEvent)))
            {
                transportConfig.MapEvent<Sagas.When_started_by_base_event_from_other_saga.BaseEvent, Sagas.When_started_by_base_event_from_other_saga.SomethingHappenedEvent>();
            }

            if (endpointName == Conventions.EndpointNamingConvention(typeof(Versioning.When_multiple_versions_of_a_message_is_published.V1Subscriber)))
            {
                transportConfig.MapEvent<Versioning.When_multiple_versions_of_a_message_is_published.V1Event, Versioning.When_multiple_versions_of_a_message_is_published.V2Event>();
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