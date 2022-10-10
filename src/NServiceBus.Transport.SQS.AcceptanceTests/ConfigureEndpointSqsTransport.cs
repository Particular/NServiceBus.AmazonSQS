namespace NServiceBus.AcceptanceTests
{
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using ScenarioDescriptors;
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using MessageMutator;
    using Microsoft.Extensions.DependencyInjection;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        const string S3BucketEnvironmentVariableName = "NSERVICEBUS_AMAZONSQS_S3BUCKET";
        public const string S3Prefix = "test";
        public static string S3BucketName;
        readonly bool supportsPublishSubscribe;

        public ConfigureEndpointSqsTransport(bool supportsPublishSubscribe = true)
        {
            this.supportsPublishSubscribe = supportsPublishSubscribe;
        }

        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            var transport = PrepareSqsTransport(supportsPublishSubscribe);
            configuration.UseTransport(transport);

            //We set the default test execution timeout only when not explicitly set by the test
            if (settings.TestExecutionTimeout == null || settings.TestExecutionTimeout.Value <= TimeSpan.FromSeconds(120))
            {
                settings.TestExecutionTimeout = TimeSpan.FromSeconds(120);
            }

            configuration.RegisterComponents(c => c.AddSingleton<IMutateOutgoingTransportMessages, TestIndependenceMutator>());
            configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");

            return Task.FromResult(0);
        }

        public static SqsTransport PrepareSqsTransport(bool supportsPublishSubscribe = true)
        {
            var transport = new SqsTransport(CreateSqsClient(), CreateSnsClient(), supportsPublishSubscribe)
            {
                QueueNamePrefix = SetupFixture.NamePrefix,
                TopicNamePrefix = SetupFixture.NamePrefix,
                QueueNameGenerator = TestNameHelper.GetSqsQueueName,
                TopicNameGenerator = TestNameHelper.GetSnsTopicName
            };

            S3BucketName = EnvironmentHelper.GetEnvironmentVariable(S3BucketEnvironmentVariableName);

            if (!string.IsNullOrWhiteSpace(S3BucketName))
            {
                transport.S3 = new S3Settings(S3BucketName, S3Prefix, CreateS3Client());
            }

            return transport;
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
    }
}