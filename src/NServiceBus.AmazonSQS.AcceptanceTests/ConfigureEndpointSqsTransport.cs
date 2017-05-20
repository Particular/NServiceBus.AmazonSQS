using Amazon.SQS;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.AmazonSQS;
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
                .QueueNamePrefix("AcceptanceTest-");
            
            var routingConfig = transportConfig.Routing();

            foreach (var publisher in publisherMetadata.Publishers)
            {
                foreach (var eventType in publisher.Events)
                {
                    routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
                }
            }

            return Task.FromResult(0);
        }

        public async Task Cleanup()
        {/*
            var connectionConfig = SqsConnectionStringParser.Parse(_connectionString);
            var sqsClient = AwsClientFactory.CreateSqsClient(connectionConfig);
            var listQueuesResponse = await sqsClient.ListQueuesAsync(connectionConfig.QueueNamePrefix);
            foreach( var queue in listQueuesResponse.QueueUrls)
            {
                if (queue.Contains(connectionConfig.QueueNamePrefix + "error"))
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
        }
    }
}
