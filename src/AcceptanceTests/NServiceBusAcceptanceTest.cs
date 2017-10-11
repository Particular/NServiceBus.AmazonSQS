﻿namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AmazonSQS;
    using AmazonSQS.AcceptanceTests;
    using Configuration.AdvancedExtensibility;
    using NUnit.Framework;
    using Settings;

    [SetUpFixture]
    public abstract partial class NServiceBusAcceptanceTest
    {
        // The queue name prefix for the current run of the test suite.
        // Generate a new queue name prefix for acceptance tests
        // every time the tests are run.
        // This is to work around an SQS limitation that prevents
        // us from deleting then creating a queue with the
        // same name in a 60 second period.
        public static string SqsQueueNamePrefix = $"AT{DateTime.Now:yyyyMMddHHmmss}";

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            // Once all tests have completed, delete all queues that were created.
            // Use the QueueNamePrefix to determine which queues to delete.
            var transportConfiguration = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transportConfiguration = transportConfiguration.ConfigureSqsTransport(SqsQueueNamePrefix);
            var connectionConfiguration = new ConnectionConfiguration(transportConfiguration.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var listQueuesResult = await sqsClient.ListQueuesAsync(connectionConfiguration.QueueNamePrefix).ConfigureAwait(false);
                foreach (var queueUrl in listQueuesResult.QueueUrls)
                {
                    try
                    {
                        await sqsClient.DeleteQueueAsync(queueUrl).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception when deleting queue: {ex}");
                    }
                }
            }
        }
    }
}