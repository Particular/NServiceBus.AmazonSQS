namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport.SQS.Tests;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The name prefix for the current run of the test suite.
        /// </summary>
        public static string NamePrefix { get; private set; }

        public static void AppendToNamePrefix(string customization) => NamePrefix += customization;

        public static void RestoreNamePrefix(string namePrefixBackup)
        {
            if (!string.IsNullOrWhiteSpace(namePrefixBackup))
            {
                NamePrefix = namePrefixBackup;
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a new name prefix for acceptance tests
            // every time the tests are run.
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the
            // same name in a 60 second period.
            NamePrefix = $"AT{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "").ToUpperInvariant()}";
            TestContext.WriteLine($"Generated name prefix: '{NamePrefix}'");
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            using var sqsClient = ClientFactories.CreateSqsClient();
            using var snsClient = ClientFactories.CreateSnsClient();
            using var s3Client = ClientFactories.CreateS3Client();

            await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefix).ConfigureAwait(false);
        }
    }
}