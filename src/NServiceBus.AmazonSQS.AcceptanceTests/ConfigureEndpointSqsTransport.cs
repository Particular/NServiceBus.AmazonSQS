﻿using Amazon.SQS;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.AmazonSQS;
using System.Threading.Tasks;

namespace NServiceBus.AcceptanceTests
{
    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        static string DefaultConnectionString = "Region=ap-southeast-2;S3BucketForLargeMessages=sqstransportmessages1337;S3KeyPrefix=test;QueueNamePrefix=AcceptanceTest-;TruncateLongQueueNames=true";

        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            _connectionString =
                EnvironmentHelper.GetEnvironmentVariable($"{nameof(SqsTransport)}.ConnectionString")
                ?? DefaultConnectionString;
            var transportConfig = configuration.UseTransport<SqsTransport>();

            transportConfig.ConnectionString(_connectionString);

            return Task.FromResult(0);
        }

        public async Task Cleanup()
        {
            var connectionConfig = SqsConnectionStringParser.Parse(_connectionString);
            var sqsClient = AwsClientFactory.CreateSqsClient(connectionConfig);
            var listQueuesResponse = await sqsClient.ListQueuesAsync(connectionConfig.QueueNamePrefix);
            foreach( var queue in listQueuesResponse.QueueUrls)
            {
                try
                {
                    await sqsClient.DeleteQueueAsync(queue);
                }
                catch(AmazonSQSException)
                {
                    // Probably just trying to delete a queue that was already deleted
                }
            }
        }

        string _connectionString;
    }
}
