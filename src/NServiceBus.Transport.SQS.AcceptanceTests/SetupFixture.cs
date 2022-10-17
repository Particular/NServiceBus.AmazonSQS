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
        public static string NamePrefix { get; set; }

        //TODO: This could go away entirely
        //static bool usingFixedNamePrefix;
        //static string namePrefixBackup;

        //TODO: This could go away entirely
        //static string GetFixedNamePrefix()
        //{
        //    var fixedNamePrefixKeyName = "NServiceBus_AmazonSQS_AT_CustomFixedNamePrefix";
        //    var customFixedNamePrefix = EnvironmentHelper.GetEnvironmentVariable(fixedNamePrefixKeyName);

        //    if (customFixedNamePrefix == null)
        //    {
        //        throw new Exception($"Environment variable '{fixedNamePrefixKeyName}' not set. " +
        //            $"The variable is required by tests bound to a fixed infrastructure. " +
        //            $"Make sure the value doesn't contain any space or dash character.");
        //    }
        //    else if (customFixedNamePrefix.Contains(".") || customFixedNamePrefix.Contains("-"))
        //    {
        //        throw new Exception($"Environment variable '{fixedNamePrefixKeyName}' contains " +
        //                            $"invalid characters. Current value is: '{customFixedNamePrefix}'" +
        //                            $"Make sure the value doesn't contain any space or dash character.");
        //    }

        //    return customFixedNamePrefix;
        //}

        //TODO: This could go away entirely
        //public static void UseFixedNamePrefix()
        //{
        //    usingFixedNamePrefix = true;

        //    namePrefixBackup = NamePrefix;
        //    NamePrefix = GetFixedNamePrefix();

        //    TestContext.WriteLine($"Using fixed name prefix: '{NamePrefix}'");
        //}

        //TODO: This could go away entirely
        //public static void RestoreNamePrefixToRandomlyGenerated()
        //{
        //    if (usingFixedNamePrefix)
        //    {
        //        TestContext.WriteLine($"Restoring name prefix from '{NamePrefix}' to '{namePrefixBackup}'");
        //        NamePrefix = namePrefixBackup;
        //        usingFixedNamePrefix = false;
        //    }
        //}

        //TODO: This could go away entirely
        //public static void AppendSequenceToNamePrefix(int sequence)
        //{
        //    var idx = NamePrefix.LastIndexOf('-');
        //    if (idx >= 0)
        //    {
        //        NamePrefix = NamePrefix.Substring(0, idx);
        //    }
        //    NamePrefix += $"-{sequence}";

        //    TestContext.WriteLine($"Sequence #{sequence} appended name prefix: '{NamePrefix}'");
        //}

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
            var accessKeyId = EnvironmentHelper.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var secretAccessKey = EnvironmentHelper.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

            using (var sqsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSQSClient() :
                new AmazonSQSClient(accessKeyId, secretAccessKey))
            using (var snsClient = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateSnsClient() :
                new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey))
            using (var s3Client = string.IsNullOrEmpty(accessKeyId) ? SqsTransportExtensions.CreateS3Client() :
                new AmazonS3Client(accessKeyId, secretAccessKey))
            {
                //TODO: This could go away entirely
                // var idx = NamePrefix.LastIndexOf('-');
                // if (idx >= 0)
                // {
                //     //remove the sequence number before cleaning up
                //     NamePrefix = NamePrefix.Substring(0, idx);
                // }

                await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, NamePrefix).ConfigureAwait(false);
            }
        }
    }
}