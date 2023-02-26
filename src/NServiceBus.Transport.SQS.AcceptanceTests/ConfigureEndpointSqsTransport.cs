namespace NServiceBus.AcceptanceTests
{
    using ScenarioDescriptors;
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using NUnit.Framework;
    using Routing;
    using Routing.NativePublishSubscribe;
    using Sagas;
    using Transport.SQS.Tests;
    using Versioning;
    using MessageDriven = Routing.MessageDrivenSubscriptions;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        const string S3BucketEnvironmentVariableName = "NSERVICEBUS_AMAZONSQS_S3BUCKET";
        public const string S3Prefix = "test";
        public static string S3BucketName;
        readonly bool supportsPublishSubscribe;

        public ConfigureEndpointSqsTransport(bool supportsPublishSubscribe = true)
            => this.supportsPublishSubscribe = supportsPublishSubscribe;

        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            PreventInconclusiveTestsFromRunning(endpointName);

            var transport = PrepareSqsTransport(supportsPublishSubscribe);
            configuration.UseTransport(transport);

            //We set the default test execution timeout only when not explicitly set by the test
            if (settings.TestExecutionTimeout == null || settings.TestExecutionTimeout.Value <= TimeSpan.FromSeconds(120))
            {
                settings.TestExecutionTimeout = TimeSpan.FromSeconds(120);
            }

            ApplyMappingsToSupportMultipleInheritance(endpointName, transport);

            return Task.CompletedTask;
        }

        public static SqsTransport PrepareSqsTransport(bool supportsPublishSubscribe = true)
        {
            var transport = new SqsTransport(ClientFactories.CreateSqsClient(), ClientFactories.CreateSnsClient(), supportsPublishSubscribe)
            {
                QueueNamePrefix = SetupFixture.NamePrefix,
                TopicNamePrefix = SetupFixture.NamePrefix,
                QueueNameGenerator = TestNameHelper.GetSqsQueueName,
                TopicNameGenerator = TestNameHelper.GetSnsTopicName
            };

            S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrWhiteSpace(S3BucketName))
            {
                transport.S3 = new S3Settings(S3BucketName, S3Prefix, ClientFactories.CreateS3Client());
            }

            return transport;
        }

        static void ApplyMappingsToSupportMultipleInheritance(string endpointName, SqsTransport transportConfig)
        {
            if (endpointName == Conventions.EndpointNamingConvention(typeof(MultiSubscribeToPolymorphicEvent.Subscriber)))
            {
                transportConfig.MapEvent<MultiSubscribeToPolymorphicEvent.IMyEvent, MultiSubscribeToPolymorphicEvent.MyEvent1>();
                transportConfig.MapEvent<MultiSubscribeToPolymorphicEvent.IMyEvent, MultiSubscribeToPolymorphicEvent.MyEvent2>();
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

        public Task Cleanup() =>
            // Queues are cleaned up once, globally, in SetupFixture.
            Task.CompletedTask;

        static void PreventInconclusiveTestsFromRunning(string endpointName)
        {
            if (endpointName == Conventions.EndpointNamingConvention(typeof(MessageDriven.Pub_from_sendonly.SendOnlyPublisher)))
            {
                Assert.Inconclusive("Test is not using endpoint naming conventions in hardcoded subscription storage. Should be fixed in core vNext.");
            }
        }
    }
}