using NServiceBus.AcceptanceTesting.Support;
using System;
using System.Threading.Tasks;

namespace NServiceBus.AcceptanceTests
{
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
            return Task.FromResult(0);
        }

        public Task Cleanup()
        {/*
            var sqsConnectionConfiguration = new SqsConnectionConfiguration(_settings);
            var sqsClient = AwsClientFactory.CreateSqsClient(sqsConnectionConfiguration);
            var listQueuesResponse = await sqsClient.ListQueuesAsync(sqsConnectionConfiguration.QueueNamePrefix);
            foreach( var queue in listQueuesResponse.QueueUrls)
            {
                if (queue.Contains(sqsConnectionConfiguration.QueueNamePrefix + "error"))
                    continue;

                try
                {
                    await sqsClient.DeleteQueueAsync(queue);
                }
                catch(AmazonSQSException)
                {
                    // Probably just trying to delete a queue that was already deleted
                }
            }*/
            return Task.FromResult(0);
        }
    }
}
