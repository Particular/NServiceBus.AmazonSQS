namespace NServiceBus.AcceptanceTests;

using System;
using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Transport.SQS.Tests;

[SetUpFixture]
public class SetupFixture
{
    static readonly AsyncLocal<string> customization = new();

    /// <summary>
    /// The name prefix for the current run of the test suite, including the fixture of the running test and its customization.
    /// </summary>
    public static string NamePrefix => runPrefix + FixtureToken() + customization.Value;

    // Shared tests hard-code some endpoint names, so fixtures running in parallel need their own queues and topics.
    static string FixtureToken()
    {
        var className = TestContext.CurrentContext.Test.ClassName;
        return string.IsNullOrEmpty(className) ? string.Empty : (XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(className)) & 0xFFFFFF).ToString("X6");
    }

    // AsyncLocal so fixtures running in parallel don't see each other's customization.
    public static string SetCustomization(string value)
    {
        var previous = customization.Value;
        customization.Value = previous + value;
        return previous;
    }

    public static void RestoreCustomization(string previous) => customization.Value = previous;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Generate a new name prefix for acceptance tests
        // every time the tests are run.
        // This is to work around an SQS limitation that prevents
        // us from deleting then creating a queue with the
        // same name in a 60 second period.
        // Kept short because the fixture token is added to every queue name, which is limited to 80 characters.
        runPrefix = $"AT{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray(), 8, 8), "[/+=]", "").ToUpperInvariant()}";
        TestContext.Out.WriteLine($"Generated name prefix: '{runPrefix}'");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        using var sqsClient = ClientFactories.CreateSqsClient();
        using var snsClient = ClientFactories.CreateSnsClient();
        using var s3Client = ClientFactories.CreateS3Client();

        await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, runPrefix).ConfigureAwait(false);
    }

    static string runPrefix;
}
