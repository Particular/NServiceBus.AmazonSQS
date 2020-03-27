namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using Conventions = AcceptanceTesting.Customization.Conventions;
    using AmazonSQS.AcceptanceTests;
    using NUnit.Framework;
    using Routing.MessageDrivenSubscriptions;

    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            PreventInconclusiveTestsFromRunning(endpointName);

            var transportConfig = configuration.UseTransport<SqsTransport>();

            transportConfig.ConfigureSqsTransport(SetupFixture.SqsNamePrefix);

            var routingConfig = transportConfig.Routing();
            foreach (var publisher in publisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            // TODO: remove when AWS SDK bug is resolved https://github.com/aws/aws-sdk-net/issues/796
            // The bug causes messages to be marked as in flight, but not delivered to the client.
            // Wait for tests longer than the invisibility time to make sure messages are received.
            settings.TestExecutionTimeout = TimeSpan.FromSeconds(40);

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            // Queues are cleaned up once, globally, in SetupFixture.
            return Task.FromResult(0);
        }

        static void PreventInconclusiveTestsFromRunning(string endpointName)
        {
            if (endpointName == Conventions.EndpointNamingConvention(typeof(When_publishing_from_sendonly.SendOnlyPublisher)))
            {
                Assert.Inconclusive("Test is not using endpoint naming conventions in hardcoded subscription storage. Should be fixed in core vNext.");
            }
        }
    }
}