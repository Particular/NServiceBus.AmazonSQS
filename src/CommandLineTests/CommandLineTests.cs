namespace NServiceBus.Transport.SQS.CommandLine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NUnit.Framework;
    using SQS.Tests;

    [TestFixture]
    public class CommandLineTests
    {
        [Test]
        public async Task Create_endpoint_without_prefix_when_there_are_no_entities()
        {
            var endpointName = prefix + EndpointName;

            var (_, error, exitCode) = await Execute($"endpoint create {endpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(endpointName);
        }

        [Test]
        public async Task Create_endpoint_without_prefix_when_there_are_entities()
        {
            var endpointName = prefix + EndpointName;

            await Execute($"endpoint create {endpointName}");
            var (_, error, exitCode) = await Execute($"endpoint create {endpointName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(endpointName);
        }

        [Test]
        public async Task Create_endpoint()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(EndpointName, prefix);
        }

        [Test]
        public async Task Create_endpoint_with_custom_retention()
        {
            var customRetention = 60000;
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --retention {customRetention} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(EndpointName, prefix, retentionPeriodInSeconds: customRetention);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint()
        {
            var bucketName = prefix + BucketName;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(bucketName);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint_with_custom_key_prefix()
        {
            var bucketName = prefix + BucketName;

            var keyPrefix = "k-";

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName} --key-prefix {keyPrefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(bucketName);
            await VerifyLifecycleConfiguration(bucketName, keyPrefix: keyPrefix);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint_with_custom_expiration()
        {
            var bucketName = prefix + BucketName;

            var expiration = 7;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName} --expiration {expiration}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(bucketName);
            await VerifyLifecycleConfiguration(bucketName, expiration: expiration);
        }

        [Test]
        public async Task Enable_delay_delivery_on_endpoint()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName, prefix);
        }

        [Test]
        public async Task Enable_delay_delivery_on_endpoint_with_custom_retention()
        {
            var retention = 60000;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --retention {retention} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName, prefix, retentionPeriodInSeconds: retention);
        }

        [Test]
        public async Task Subscribe_on_event()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            var queueArn = await VerifyQueue(EndpointName, prefix);
            var topicArn = await VerifyTopic(EventType, prefix);
            await VerifySubscription(topicArn, queueArn);
        }

        [Test]
        public async Task Unsubscribe_from_event()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            var queueArn = await VerifyQueue(EndpointName, prefix);
            var topicArn = await VerifyTopic(EventType, prefix);
            await VerifySubscription(topicArn, queueArn);

            await Execute($"endpoint unsubscribe {EndpointName} {EventType} --prefix {prefix}");

            await VerifySubscriptionDeleted(topicArn, queueArn);
            await VerifyTopic(EventType, prefix);
        }

        [Test]
        public async Task Unsubscribe_from_event_with_remove_shared_resources()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            var queueArn = await VerifyQueue(EndpointName, prefix);
            var topicArn = await VerifyTopic(EventType, prefix);
            await VerifySubscription(topicArn, queueArn);

            await Execute($"endpoint unsubscribe {EndpointName} {EventType} --prefix {prefix} --remove-shared-resources");

            await VerifyTopicDeleted(EventType, prefix);
        }

        [Test]
        public async Task Remove_delay_delivery_from_endpoint()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueue(EndpointName, prefix);

            (_, error, exitCode) = await Execute($"endpoint remove {EndpointName} delay-delivery-support --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyDelayDeliveryQueueDeleted(EndpointName, prefix);
        }

        [Test]
        public async Task Remove_large_message_support_from_endpoint_does_not_remove_bucket_by_default()
        {
            var bucketName = prefix + BucketName;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(bucketName);

            (_, error, exitCode) = await Execute($"endpoint remove {EndpointName} large-message-support {bucketName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(bucketName);
        }

        [Test]
        public async Task Remove_large_message_support_from_endpoint_removes_bucket_with_remove_shared_resources()
        {
            var bucketName = prefix + BucketName;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucket(bucketName);

            (_, error, exitCode) = await Execute($"endpoint remove {EndpointName} large-message-support {bucketName} --remove-shared-resources");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyBucketDeleted(bucketName);
        }

        [Test]
        public async Task Delete_endpoint()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueue(EndpointName, prefix);

            (_, error, exitCode) = await Execute($"endpoint delete {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyQueueDeleted(EndpointName, prefix);
        }

        async Task<(string output, string error, int exitCode)> Execute(string command)
        {
            var process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = TestContext.CurrentContext.TestDirectory;
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = "NServiceBus.Transports.SQS.CommandLine.dll " + command + " -i " + accessKey + " -s " + secret + " -r " + region;

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

        async Task<string> VerifyQueue(string queueName, string prefix = null, double? retentionPeriodInSeconds = null)
        {
            if (prefix == null)
            {
                prefix = DefaultConfigurationValues.QueueNamePrefix;
            }

            if (retentionPeriodInSeconds == null)
            {
                retentionPeriodInSeconds = DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
            }

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);

            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string> {QueueAttributeName.MessageRetentionPeriod, QueueAttributeName.QueueArn}).ConfigureAwait(false);

            Assert.AreEqual(retentionPeriodInSeconds, queueAttributesResponse.MessageRetentionPeriod);

            return queueAttributesResponse.QueueARN;
        }

        async Task<string> VerifyDelayDeliveryQueue(string queueName, string prefix = null, double? retentionPeriodInSeconds = null, double? delayInSeconds = null, string suffix = null)
        {
            if (prefix == null)
            {
                prefix = DefaultConfigurationValues.QueueNamePrefix;
            }

            if (retentionPeriodInSeconds == null)
            {
                retentionPeriodInSeconds = DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
            }

            if (delayInSeconds == null)
            {
                delayInSeconds = DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds;
            }

            if (suffix == null)
            {
                suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;
            }

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}{suffix}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);

            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string>
            {
                QueueAttributeName.MessageRetentionPeriod,
                QueueAttributeName.DelaySeconds,
                QueueAttributeName.QueueArn
            }).ConfigureAwait(false);

            Assert.AreEqual(retentionPeriodInSeconds, queueAttributesResponse.MessageRetentionPeriod);
            Assert.AreEqual(delayInSeconds, queueAttributesResponse.DelaySeconds);

            return queueAttributesResponse.QueueARN;
        }

        async Task<string> VerifyTopic(string eventType, string prefix = null)
        {
            if (prefix == null)
            {
                prefix = DefaultConfigurationValues.TopicNamePrefix;
            }

            var topicName = TopicSanitization.GetSanitizedTopicName($"{prefix}{eventType}");

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
            if (keyPrefix == null)
            {
                keyPrefix = DefaultConfigurationValues.S3KeyPrefix;
            }

            if (expiration == null)
            {
                expiration = (int)Math.Ceiling(DefaultConfigurationValues.RetentionPeriod.TotalDays);
            }

            LifecycleRule setLifeCycleConfig;
            int backOff;
            var executions = 0;
            do
            {
                backOff = executions * executions * verificationBackoffInterval;
                await Task.Delay(backOff);
                executions++;

                var lifecycleConfig = await s3.GetLifecycleConfigurationAsync(bucketName).ConfigureAwait(false);
                setLifeCycleConfig = lifecycleConfig.Configuration.Rules.FirstOrDefault(x => x.Id == "NServiceBus.SQS.DeleteMessageBodies");
            } while (setLifeCycleConfig == null && backOff < maximumBackoffInterval);

            Assert.IsNotNull(setLifeCycleConfig);
            Assert.AreEqual(expiration, setLifeCycleConfig.Expiration.Days);
            Assert.AreEqual(keyPrefix, (setLifeCycleConfig.Filter.LifecycleFilterPredicate as LifecyclePrefixPredicate).Prefix);
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

        public async Task VerifySubscriptionDeleted(string topicArn, string queueArn)
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

            Assert.IsNull(subscription);
        }

        async Task VerifyTopicDeleted(string eventType, string prefix = null)
        {
            if (prefix == null)
            {
                prefix = DefaultConfigurationValues.TopicNamePrefix;
            }

            var topicName = TopicSanitization.GetSanitizedTopicName($"{prefix}{eventType}");

            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);

            Assert.IsNull(findTopicResponse);
        }

        async Task VerifyDelayDeliveryQueueDeleted(string queueName, string prefix = null, string suffix = null)
        {
            if (prefix == null)
            {
                prefix = DefaultConfigurationValues.QueueNamePrefix;
            }

            if (suffix == null)
            {
                suffix = DefaultConfigurationValues.DelayedDeliveryQueueSuffix;
            }

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}{suffix}");
            GetQueueUrlResponse queueUrlResponse;
            var backOff = 0;
            var executions = 0;
            do
            {
                try
                {
                    backOff = executions * executions * verificationBackoffInterval;
                    await Task.Delay(backOff);
                    executions++;

                    queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                }
                catch (QueueDoesNotExistException)
                {
                    // expected
                    queueUrlResponse = null;
                }
            } while (queueUrlResponse != null && backOff < maximumBackoffInterval);

            Assert.IsNull(queueUrlResponse);
        }

        async Task VerifyQueueDeleted(string queueName, string prefix = null)
        {
            if (prefix == null)
            {
                prefix = DefaultConfigurationValues.QueueNamePrefix;
            }

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            GetQueueUrlResponse queueUrlResponse;
            var backOff = 0;
            var executions = 0;
            do
            {
                try
                {
                    backOff = executions * executions * verificationBackoffInterval;
                    await Task.Delay(backOff);
                    executions++;

                    queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                }
                catch (QueueDoesNotExistException)
                {
                    // expected
                    queueUrlResponse = null;
                }
            } while (queueUrlResponse != null && backOff < maximumBackoffInterval);

            Assert.IsNull(queueUrlResponse);
        }

        async Task VerifyBucketDeleted(string bucketName)
        {
            int backOff;
            var executions = 0;
            bool bucketExists;
            do
            {
                backOff = executions * executions * verificationBackoffInterval;
                await Task.Delay(backOff);
                executions++;

                bucketExists = await s3.DoesS3BucketExistAsync(bucketName);
            } while (bucketExists && backOff < maximumBackoffInterval);

            Assert.IsFalse(bucketExists);
        }

        [SetUp]
        public void Setup()
        {
            prefix = $"cli-{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "").ToLowerInvariant()}-";

            var regionEndpoint = RegionEndpoint.GetBySystemName(region);

            sqs = new AmazonSQSClient(accessKey, secret, regionEndpoint);
            sns = new AmazonSimpleNotificationServiceClient(accessKey, secret, regionEndpoint);
            s3 = new AmazonS3Client(accessKey, secret, regionEndpoint);
        }

        [TearDown]
        public async Task TearDown()
        {
            using (sqs)
            using (sns)
            using (s3)
            {
                await Cleanup.DeleteAllResourcesWithPrefix(sqs, sns, s3, prefix).ConfigureAwait(false);
            }
        }

        string prefix;

        string accessKey = Environment.GetEnvironmentVariable("CLEANUP_AWS_ACCESS_KEY_ID");
        string secret = Environment.GetEnvironmentVariable("CLEANUP_AWS_SECRET_ACCESS_KEY");
        string region = Environment.GetEnvironmentVariable("AWS_REGION");

        private IAmazonSQS sqs;
        private IAmazonSimpleNotificationService sns;
        private IAmazonS3 s3;
        const string EndpointName = "nsb-cli-test";
        const string BucketName = "nsb-cli-test-bucket";
        const string EventType = "MyNamespace.MyMessage1";
        const int verificationBackoffInterval = 200;
        const int maximumBackoffInterval = 20000; // totals up to 77000
    }
}