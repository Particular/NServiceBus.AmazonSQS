using System.Threading.Tasks;

namespace NServiceBus.TransportTests
{
    using NServiceBus.AmazonSQS;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using System;
    using System.Linq;

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
            SqsQueueNamePrefix = $"TT{DateTime.Now:yyyyMMddHHmmss}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            // Once all tests have completed, delete all queues that were created.
            // Use the QueueNamePrefix to determine which queues to delete.
            var transportConfiguration = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transportConfiguration = ConfigureSqsTransportInfrastructure.DefaultConfigureSqs(transportConfiguration);
            var connectionConfiguration = new SqsConnectionConfiguration(transportConfiguration.GetSettings());
            var sqsClient = AwsClientFactory.CreateSqsClient(connectionConfiguration);
            var listQueuesResult = await sqsClient.ListQueuesAsync(connectionConfiguration.QueueNamePrefix).ConfigureAwait(false);
            var deleteTasks = listQueuesResult.QueueUrls.Select(x => sqsClient.DeleteQueueAsync(x));
            // Occasionally DeleteQueueAsync fails and throws, leaving orphaned queues.
            // No need to explicitly handle exceptions here, they do not cause the tests to fail.
            await Task.WhenAll(deleteTasks).ConfigureAwait(false);
        }
    }
}
