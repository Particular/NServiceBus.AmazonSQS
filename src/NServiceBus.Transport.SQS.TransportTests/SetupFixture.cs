namespace TransportTests;

using System;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using NServiceBus.Transport.SQS.Tests;
using NUnit.Framework;

[SetUpFixture]
public class SetupFixture
{
    /// <summary>
    ///     The name prefix for the current run of the test suite.
    /// </summary>
    static string NamePrefix { get; set; }

    public static string GetNamePrefix()
    {
        TestContext ctx = TestContext.CurrentContext;
        int methodHashPositive = Math.Abs(ctx.Test.ID.GetHashCode());

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
        using IAmazonSQS sqsClient = ClientFactories.CreateSqsClient();
        using IAmazonSimpleNotificationService snsClient = ClientFactories.CreateSnsClient();
        using IAmazonS3 s3Client = ClientFactories.CreateS3Client();

        await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefix).ConfigureAwait(false);
    }
}