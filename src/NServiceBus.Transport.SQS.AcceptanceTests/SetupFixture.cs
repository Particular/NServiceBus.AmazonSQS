namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Amazon.S3;
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

        static bool usingPermanentNamePrefix;

        static string GetFixedNamePrefix()
        {
            var fixedNamePrefixKeyName = "NServiceBus_AmazonSQS_AT_CustomFixedNamePrefix";
            var customFixedNamePrefix = EnvironmentHelper.GetEnvironmentVariable(fixedNamePrefixKeyName);

            if (customFixedNamePrefix == null)
            {
                throw new Exception($"Environment variable {fixedNamePrefixKeyName} not set. It's needed to setup tests using predefined name prefixes");
            }

            return customFixedNamePrefix;
        }

        public static void UsePermanentNamePrefix(string testCasePrefix)
        {
            usingPermanentNamePrefix = true;

            NamePrefix = $"{GetFixedNamePrefix()}-{testCasePrefix}-";

            TestContext.WriteLine($"Using fixed name prefix: '{NamePrefix}'");
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

        public static async Task PurgeQueues()
        {
            if (usingPermanentNamePrefix)
            {
                var accessKeyId = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_ACCESS_KEY_ID");
                var secretAccessKey = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_SECRET_ACCESS_KEY");

                using (var sqsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSQSClient() : new AmazonSQSClient(accessKeyId, secretAccessKey))
                {
                    await Cleanup.PurgeAllQueuesWithPrefix(sqsClient, GetFixedNamePrefix());
                }
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (usingPermanentNamePrefix == false)
            {
                var accessKeyId = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_ACCESS_KEY_ID");
                var secretAccessKey = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_SECRET_ACCESS_KEY");

                using (var sqsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSQSClient() : new AmazonSQSClient(accessKeyId, secretAccessKey))
                using (var snsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSnsClient() : new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey))
                using (var s3Client = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateS3Client() : new AmazonS3Client(accessKeyId, secretAccessKey))
                {
                    await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefix).ConfigureAwait(false);
                }
            }

        }
    }
}