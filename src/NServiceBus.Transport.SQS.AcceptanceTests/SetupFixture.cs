namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NUnit.Framework;
    using ScenarioDescriptors;
    using Transport.SQS.Tests;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The name prefix for the current run of the test suite.
        /// </summary>
        public static string NamePrefix { get; private set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a new name prefix for acceptance tests
            // every time the tests are run.
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the
            // same name in a 60 second period.
            NamePrefix = $"AT{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "").ToUpperInvariant()}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            var accessKeyId = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_ACCESS_KEY_ID");
            var secretAccessKey = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_SECRET_ACCESS_KEY");

            using (var sqsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSQSClient() :
                new AmazonSQSClient(accessKeyId, secretAccessKey))
            using (var snsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSnsClient() :
                new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey))
            {
                await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, NamePrefix).ConfigureAwait(false);
            }
        }
    }
}