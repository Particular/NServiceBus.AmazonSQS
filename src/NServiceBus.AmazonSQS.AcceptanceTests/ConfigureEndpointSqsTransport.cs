using NServiceBus.AcceptanceTesting.Support;
using System;
using System.Threading.Tasks;

namespace NServiceBus.AcceptanceTests
{
    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        public static TransportExtensions<SqsTransport> DefaultConfigureSqs(TransportExtensions<SqsTransport> transportConfiguration)
        {
            transportConfiguration
                .Region("ap-southeast-2")
                .S3BucketForLargeMessages("sqstransportmessages1337", "test")
                .QueueNamePrefix(SetupFixture.SqsQueueNamePrefix)
                .NativeDeferral()
                .PreTruncateQueueNamesForAcceptanceTests();
            return transportConfiguration;
        }

        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            var transportConfig = configuration.UseTransport<SqsTransport>();

            DefaultConfigureSqs(transportConfig);
            
            var routingConfig = transportConfig.Routing();

            foreach (var publisher in publisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            settings.TestExecutionTimeout = TimeSpan.FromSeconds(20);
            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            // Queues are cleaned up once, globally, in SetupFixture.
            return Task.FromResult(0);
        }
    }
}
