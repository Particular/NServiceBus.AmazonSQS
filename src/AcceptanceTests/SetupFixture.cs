namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService.Model;
    using AmazonSQS;
    using AmazonSQS.AcceptanceTests;
    using Configuration.AdvancedExtensibility;
    using NUnit.Framework;
    using Settings;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The name prefix for the current run of the test suite.
        /// </summary>
        public static string SqsNamePrefix { get; private set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a new name prefix for acceptance tests
            // every time the tests are run.
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the
            // same name in a 60 second period.
            SqsNamePrefix = $"AT{DateTime.Now:yyyyMMddHHmmss}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            // Once all tests have completed, delete all queues that were created.
            // Use the QueueNamePrefix to determine which queues to delete.
            var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
            transport = transport.ConfigureSqsTransport(SqsNamePrefix);
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

            using (var snsClient = SqsTransportExtensions.CreateSnsClient())
            {
                ListTopicsResponse listTopicsResult = null;
                do
                {
                    listTopicsResult = await snsClient.ListTopicsAsync().ConfigureAwait(false);
                    foreach (var topic in listTopicsResult.Topics)
                    {
                        if (topic.TopicArn.Contains(":" + SqsNamePrefix))
                        {
                            try
                            {
                                await snsClient.DeleteTopicAsync(topic.TopicArn).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Exception when deleting topic: {ex}");
                            }
                        }

                    }
                }
                while (listTopicsResult?.NextToken != null);

            }
        }
    }
}