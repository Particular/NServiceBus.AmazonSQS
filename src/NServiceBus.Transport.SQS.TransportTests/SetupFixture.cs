namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTests.ScenarioDescriptors;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using NUnit.Framework;
    using Transport.SQS.Tests;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The name prefix for the current run of the test suite.
        /// </summary>
        static string NamePrefix { get; set; }

        public static string GetNamePrefix()
        {
            var ctx = TestContext.CurrentContext;
            var methodName = ctx.Test.MethodName;
            var methodHashPositive = Math.Abs(methodName.GetHashCode());

            return NamePrefix + methodHashPositive.ToString();
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a new queue name prefix for acceptance tests
            // every time the tests are run.
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the
            // same name in a 60 second period.
            NamePrefix = $"TT{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            var accessKeyId = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_ACCESS_KEY_ID");
            var secretAccessKey = EnvironmentHelper.GetEnvironmentVariable("CLEANUP_AWS_SECRET_ACCESS_KEY");

            using (var sqsClient = string.IsNullOrEmpty(accessKeyId) ? ConfigureSqsTransportInfrastructure.CreateSqsClient() :
                new AmazonSQSClient(accessKeyId, secretAccessKey))
            using (var snsClient = string.IsNullOrEmpty(accessKeyId) ? ConfigureSqsTransportInfrastructure.CreateSnsClient() :
                new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey))
            using (var s3Client = string.IsNullOrEmpty(accessKeyId) ? ConfigureSqsTransportInfrastructure.CreateS3Client() :
                new AmazonS3Client(accessKeyId, secretAccessKey))
            {
                await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefix).ConfigureAwait(false);
            }
        }
    }
}