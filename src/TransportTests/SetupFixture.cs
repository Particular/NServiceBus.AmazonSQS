namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using AmazonSQS;
    using AmazonSQS.AcceptanceTests;
    using Configuration.AdvancedExtensibility;
    using NUnit.Framework;
    using Settings;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The queue name prefix for the current run of the test suite.
        /// </summary>
        public static string SqsQueueNamePrefix { get; private set; }

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
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SqsQueueNamePrefix);
            var transportConfiguration = new TransportConfiguration(transport.GetSettings());
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            {
                var listQueuesResult = await sqsClient.ListQueuesAsync(transportConfiguration.QueueNamePrefix).ConfigureAwait(false);
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