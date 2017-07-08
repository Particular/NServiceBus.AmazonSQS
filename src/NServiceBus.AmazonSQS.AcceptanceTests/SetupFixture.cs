using System.Threading.Tasks;

namespace NServiceBus.AcceptanceTests
{
    using NServiceBus.AmazonSQS;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using System;
    using NServiceBus.AmazonSQS.AcceptanceTests;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The queue name prefix for the current run of the test suite.
        /// </summary>
        public static string SqsQueueNamePrefix
        {
            get;
            private set;
        }
     
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a new queue name prefix for acceptance tests
            // every time the tests are run. 
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the 
            // same name in a 60 second period.
            SqsQueueNamePrefix = $"AT{DateTime.Now:yyyyMMddHHmmss}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            // Once all tests have completed, delete all queues that were created.
            // Use the QueueNamePrefix to determine which queues to delete.
            var transportConfiguration = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transportConfiguration = transportConfiguration.ConfigureSqsTransport(SqsQueueNamePrefix);
            var connectionConfiguration = new SqsConnectionConfiguration(transportConfiguration.GetSettings());
            var sqsClient = AwsClientFactory.CreateSqsClient(connectionConfiguration);
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
