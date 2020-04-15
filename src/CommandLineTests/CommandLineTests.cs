namespace NServiceBus.Transport.SQS.CommandLine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NUnit.Framework;

    [TestFixture]
    public class CommandLineTests
    {
        const string EndpointName = "nsb-cli-test";
        const string BucketName = "nsb-cli-test-bucket";
        const string EventType = "MyNamespace.MyMessage1";

        [Test]
        public async Task Create_endpoint_when_there_are_no_entities()
        {
            await DeleteQueue(EndpointName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(EndpointName);
        }

        [Test]
        public async Task Create_endpoint_when_there_are_entities()
        {
            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");
            (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(EndpointName);
        }

        [Test]
        public async Task Create_endpoint_with_custom_retention()
        {
            await DeleteQueue(EndpointName);

            var customRetention = 60000;
            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName} --retention {customRetention}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(EndpointName, customRetention);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint()
        {
            await DeleteBucket(BucketName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {BucketName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(BucketName);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint_with_custom_key_prefix()
        {
            var keyPrefix = "prefix-";

            await DeleteBucket(BucketName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {BucketName} --key-prefix {keyPrefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(BucketName);
            await VerifyLifecycleConfiguration(BucketName, keyPrefix: keyPrefix);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint_with_custom_expiration()
        {
            var expiration = 7;

            await DeleteBucket(BucketName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {BucketName} --expiration {expiration}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(BucketName);
            await VerifyLifecycleConfiguration(BucketName, expiration: expiration);
        }
        
        [Test]
        public async Task Enable_delay_delivery_on_endpoint()
        {
            await DeleteDelayDeliveryQueue(EndpointName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName);
        }

        [Test]
        public async Task Enable_delay_delivery_on_endpoint_with_custom_delay()
        {
            var delay = 600;

            await DeleteDelayDeliveryQueue(EndpointName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --delay {delay}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName, delayInSeconds: delay);
        }

        [Test]
        public async Task Enable_delay_delivery_on_endpoint_with_custom_retention()
        {
            var retention = 60000;

            await DeleteDelayDeliveryQueue(EndpointName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --retention {retention}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName, retentionPeriodInSeconds: retention);
        }

        [Test]
        public async Task Enable_delay_delivery_on_endpoint_with_custom_suffix()
        {
            var suffix = "-mydelay.fifo";

            await DeleteDelayDeliveryQueue(EndpointName, suffix);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --suffix {suffix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName, suffix: suffix);
        }

        [Test]
        public async Task Subscribe_on_event()
        {
            await DeleteQueue(EndpointName);

            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            var queueArn = await VerifyQueue(EndpointName);
            var topicArn = await VerifyTopic(EventType);
            await VerifySubscription(topicArn, queueArn);
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

        
        async Task<string> VerifyQueue(string queueName, double? retentionPeriodInSeconds = null)
        {
            if (retentionPeriodInSeconds == null) retentionPeriodInSeconds = DefaultConfigurationValues.RetentionPeriod.TotalSeconds;

            var getQueueUrlRequest = new GetQueueUrlRequest(queueName);
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);

            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string> { QueueAttributeName.MessageRetentionPeriod, QueueAttributeName.QueueArn }).ConfigureAwait(false);
                
            Assert.AreEqual(retentionPeriodInSeconds, queueAttributesResponse.MessageRetentionPeriod);

            return queueAttributesResponse.QueueARN;
        }

        async Task<string> VerifyDelayDeliveryQueue(string queueName, double? retentionPeriodInSeconds = null, double? delayInSeconds = null, string suffix = null)
        {
            if (retentionPeriodInSeconds == null) retentionPeriodInSeconds = DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
            if (delayInSeconds == null) delayInSeconds = DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds;
            if (suffix == null) suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

            var getQueueUrlRequest = new GetQueueUrlRequest(queueName + suffix);
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);

            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string> { 
                QueueAttributeName.MessageRetentionPeriod, 
                QueueAttributeName.DelaySeconds,
                QueueAttributeName.QueueArn }).ConfigureAwait(false);

            Assert.AreEqual(retentionPeriodInSeconds, queueAttributesResponse.MessageRetentionPeriod);
            Assert.AreEqual(delayInSeconds, queueAttributesResponse.DelaySeconds);

            return queueAttributesResponse.QueueARN;
        }

        async Task<string> VerifyTopic(string eventType)
        {
            var topicName = TopicSanitization.GetSanitizedTopicName(eventType);

            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);

            Assert.IsNotNull(findTopicResponse.TopicArn);

            return findTopicResponse.TopicArn;
        }

        async Task<string> VerifyBucket(string bucketName)
        {
            var listBucketsResponse = await s3.ListBucketsAsync(new ListBucketsRequest()).ConfigureAwait(false);
            var bucket = listBucketsResponse.Buckets.FirstOrDefault(x => string.Equals(x.BucketName, bucketName, StringComparison.InvariantCultureIgnoreCase));

            Assert.IsNotNull(bucket);

            return bucket.BucketName;
        }

        async Task VerifyLifecycleConfiguration(string bucketName, string keyPrefix = null, int? expiration = null)
        {
            if (keyPrefix == null) keyPrefix = DefaultConfigurationValues.S3KeyPrefix;
            if (expiration == null) expiration = (int)Math.Ceiling(DefaultConfigurationValues.RetentionPeriod.TotalDays);

            var lifecycleConfig = await s3.GetLifecycleConfigurationAsync(bucketName).ConfigureAwait(false);
            var setLifecycleConfig = lifecycleConfig.Configuration.Rules.FirstOrDefault(x => x.Id == "NServiceBus.SQS.DeleteMessageBodies");

            Assert.IsNotNull(setLifecycleConfig);
            Assert.AreEqual(expiration, setLifecycleConfig.Expiration.Days);
            Assert.AreEqual(keyPrefix, (setLifecycleConfig.Filter.LifecycleFilterPredicate as LifecyclePrefixPredicate).Prefix);
        }

        public async Task VerifySubscription(string topicArn, string queueArn)
        {
            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;
            Subscription subscription = null;

            do
            {
                upToAHundredSubscriptions = await sns.ListSubscriptionsByTopicAsync(topicArn, upToAHundredSubscriptions?.NextToken)
                    .ConfigureAwait(false);

                foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
                {
                    if (upToAHundredSubscription.Endpoint == queueArn)
                    {
                        subscription = upToAHundredSubscription;
                    }
                }
            } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            Assert.IsNotNull(subscription);
        }

        async Task DeleteQueue(string queueName)
        {
            try
            {
                var getQueueUrlRequest = new GetQueueUrlRequest(queueName);
                var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                var deleteRequest = new DeleteQueueRequest { QueueUrl = queueUrlResponse.QueueUrl };
                await sqs.DeleteQueueAsync(deleteRequest).ConfigureAwait(false);

                await Task.Delay(90000); // aws doesn't like us deleting and creating queues fast...
            }
            catch (QueueDoesNotExistException)
            {
                // this is fine
            }           
        }
        
        async Task DeleteDelayDeliveryQueue(string queueName, string suffix = null)
        {
            if (suffix == null) suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;
            try
            {
                var getQueueUrlRequest = new GetQueueUrlRequest(queueName + suffix);
                var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                var deleteRequest = new DeleteQueueRequest { QueueUrl = queueUrlResponse.QueueUrl };
                await sqs.DeleteQueueAsync(deleteRequest).ConfigureAwait(false);

                await Task.Delay(90000); // aws doesn't like us deleting and creating queues fast...
            }
            catch (QueueDoesNotExistException)
            {
                // this is fine
            }
        }

        async Task DeleteBucket(string bucketName)
        {
            var listBucketsResponse = await s3.ListBucketsAsync(new ListBucketsRequest()).ConfigureAwait(false);
            var bucketExists = listBucketsResponse.Buckets.Any(x => string.Equals(x.BucketName, bucketName, StringComparison.InvariantCultureIgnoreCase));
            if (bucketExists)
            {
                await s3.DeleteBucketAsync(new DeleteBucketRequest { BucketName = bucketName }).ConfigureAwait(false);

                await Task.Delay(90000); // aws doesn't like us deleting and creating buckets fast...
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