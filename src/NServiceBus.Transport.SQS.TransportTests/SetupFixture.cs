namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
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
            var methodHashPositive = Math.Abs(methodName!.GetHashCode());

            return NamePrefix + methodHashPositive;
        }

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            // Generate a new queue name prefix for acceptance tests
            // every time the tests are run.
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the
            // same name in a 60 second period.
            NamePrefix = $"TT{DateTime.UtcNow:yyyyMMddHHmmss}";

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