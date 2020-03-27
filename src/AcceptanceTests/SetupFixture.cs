﻿namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AmazonSQS.AcceptanceTests;
    using AmazonSQS.Tests;
    using NUnit.Framework;

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
            NamePrefix = $"AT{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            using (var snsClient = SqsTransportExtensions.CreateSnsClient())
            {
                await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, NamePrefix).ConfigureAwait(false);
            }
        }
    }
}