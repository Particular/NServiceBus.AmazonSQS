namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NUnit.Framework;
    using Routing;
    using Routing.NativePublishSubscribe;
    using Sagas;
    using ScenarioDescriptors;
    using Versioning;
    using MessageDriven = Routing.MessageDrivenSubscriptions;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        const string S3BucketEnvironmentVariableName = "NServiceBus_AmazonSQS_S3Bucket";
        public const string S3Prefix = "test";
        public static string S3BucketName;


        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            PreventInconclusiveTestsFromRunning(endpointName);

            var transport = PrepareSqsTransport();
            configuration.UseTransport(transport);

            settings.TestExecutionTimeout = TimeSpan.FromSeconds(120);
            ApplyMappingsToSupportMultipleInheritance(endpointName, transport);


            return Task.FromResult(0);
        }

        public static SqsTransport PrepareSqsTransport()
        {
            var transport = new SqsTransport(CreateSqsClient(), CreateSnsClient())
            {
                QueueNamePrefix = SetupFixture.NamePrefix,
                TopicNamePrefix = SetupFixture.NamePrefix,
                QueueNameGenerator = TestNameHelper.GetSqsQueueName,
                TopicNameGenerator = TestNameHelper.GetSnsTopicName
            };

            S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrEmpty(S3BucketName))
            {
                transport.S3 = new S3Settings(S3BucketName, S3Prefix, CreateS3Client());
            }

            return transport;
        }

        static void ApplyMappingsToSupportMultipleInheritance(string endpointName, SqsTransport transportConfig)
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

            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_publishing_an_event_implementing_two_unrelated_interfaces_with_AutoSubscribe.Subscriber)))
            {
                transportConfig.MapEvent<When_publishing_an_event_implementing_two_unrelated_interfaces_with_AutoSubscribe.IEventA, When_publishing_an_event_implementing_two_unrelated_interfaces_with_AutoSubscribe.CompositeEvent>();
                transportConfig.MapEvent<When_publishing_an_event_implementing_two_unrelated_interfaces_with_AutoSubscribe.IEventB, When_publishing_an_event_implementing_two_unrelated_interfaces_with_AutoSubscribe.CompositeEvent>();
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

        public static IAmazonSQS CreateSqsClient()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSQSClient(credentials);
        }

        public static IAmazonSimpleNotificationService CreateSnsClient()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonSimpleNotificationServiceClient(credentials);
        }

        public static IAmazonS3 CreateS3Client()
        {
            var credentials = new EnvironmentVariablesAWSCredentials();
            return new AmazonS3Client(credentials);
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