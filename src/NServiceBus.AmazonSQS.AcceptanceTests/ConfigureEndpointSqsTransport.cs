namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTests.Infrastructure;
    using NServiceBus.AcceptanceTesting.Support;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            var transportConfig = configuration.UseTransport<SqsTransport>();
            transportConfig
                .Region("ap-southeast-2")
                .S3BucketForLargeMessages("sqstransportmessages1337", "test")
                .QueueNamePrefix("AcceptanceTest-")
                .NativeDeferral()
                .PreTruncateQueueNamesForAcceptanceTests();

            var routingConfig = transportConfig.Routing();

            foreach (var publisher in publisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            settings.TestExecutionTimeout = TimeSpan.FromSeconds(20);

            configuration.Pipeline.Register("TestIndependenceBehavior", typeof(TestIndependenceSkipBehavior), "Skips messages not created during the current test.");
            configuration.Pipeline.Register("TestIndependenceStampBehavior", typeof(TestIndependenceStampBehavior), "Stamps messages with the test run id of the current test.");

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }
    }
}
