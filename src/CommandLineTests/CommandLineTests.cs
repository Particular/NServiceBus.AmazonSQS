namespace NServiceBus.Transport.SQS.CommandLine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NUnit.Framework;

    [TestFixture]
    public class CommandLineTests
    {
        const string EndpointName = "aws-cli";
       
        [Test]
        public async Task Create_endpoint_when_there_are_no_entities()
        {
            await DeleteQueue(EndpointName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);
            Assert.IsFalse(output.Contains("skipping"));

            await VerifyQueue(EndpointName);
        }      
     

        static async Task<(string output, string error, int exitCode)> Execute(string command)
        {
            var process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = TestContext.CurrentContext.TestDirectory;
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = "NServiceBus.Transports.SQS.CommandLine.dll " + command;

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit(10000);

            var output = await outputTask;
            var error = await errorTask;

            if (output != string.Empty)
            {
                Console.WriteLine(output);
            }

            if (error != string.Empty)
            {
                Console.WriteLine(error);
            }

            return (output, error, process.ExitCode);
        }

        
        async Task VerifyQueue(string queueName, double? retentionPeriodInSeconds = null)
        {
            if (retentionPeriodInSeconds == null) retentionPeriodInSeconds = DefaultConfigurationValues.RetentionPeriod.TotalSeconds;

            var getQueueUrlRequest = new GetQueueUrlRequest(queueName);
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);

            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string> { QueueAttributeName.MessageRetentionPeriod }).ConfigureAwait(false);
                
            Assert.AreEqual(retentionPeriodInSeconds, queueAttributesResponse.MessageRetentionPeriod);
        }      
        
        async Task DeleteQueue(string queueName)
        {
            try
            {
                var getQueueUrlRequest = new GetQueueUrlRequest(queueName);
                var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                var deleteRequest = new DeleteQueueRequest { QueueUrl = queueUrlResponse.QueueUrl };
                await sqs.DeleteQueueAsync(deleteRequest).ConfigureAwait(false);

                await Task.Delay(60000); // aws doesn't like us deleting and creating queues fast...
            }
            catch (QueueDoesNotExistException)
            {
                // this is fine
            }           
        }

        [SetUp]
        public void Setup()
        {
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            var secret = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            var region = Environment.GetEnvironmentVariable("AWS_REGION");

            var regionEndpoint = RegionEndpoint.GetBySystemName(region);

            sqs = new AmazonSQSClient(accessKey, secret, regionEndpoint);
            sns = new AmazonSimpleNotificationServiceClient(accessKey, secret, regionEndpoint);
            s3 = new AmazonS3Client(accessKey, secret, regionEndpoint);
        }

        [TearDown]
        public void Teardown()
        {
            sqs.Dispose();
            sns.Dispose();
            s3.Dispose();
        }

        private AmazonSQSClient sqs;
        private AmazonSimpleNotificationServiceClient sns;
        private AmazonS3Client s3;

    }
}