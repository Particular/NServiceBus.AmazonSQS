namespace NServiceBus.Transport.SQS.CommandLine.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.S3.Util;
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

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            await VerifyBucket(bucketName);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint_with_custom_key_prefix()
        {
            var bucketName = prefix + BucketName;

            var keyPrefix = "k-";

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName} --key-prefix {keyPrefix}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            await VerifyBucket(bucketName);
            await VerifyLifecycleConfiguration(bucketName, keyPrefix: keyPrefix);
        }

        [Test]
        public async Task Enable_large_message_support_on_endpoint_with_custom_expiration()
        {
            var bucketName = prefix + BucketName;

            var expiration = 7;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName} --expiration {expiration}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

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
        public async Task Unsubscribe_from_event_without_topic_warns()
        {
            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.IsNotNull(output);
            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint unsubscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            Assert.IsTrue(output.Contains($"No topic detected for event type '{EventType}', please subscribe to the event type first."));
        }

        [Test]
        public async Task List_policy()
        {
            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.IsNotNull(output);
            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint list-policy {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            Assert.IsNotNull(output);
            Assert.IsTrue(output.Contains("Statement"));
        }

        [Test]
        public async Task Set_policy_single_event()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} events --event-type {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyPolicyContainsTopicFor(EndpointName, prefix, EventType);
        }

        [Test]
        public async Task Set_policy_single_event_without_subscribe_warns()
        {
            var (output, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.IsNotNull(output);
            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (output, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} events --event-type {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            StringAssert.Contains($"No topic detected for event type '{EventType}', please subscribe to the event type first.", output);
        }

        [Test]
        public async Task Set_policy_multiple_events()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType2} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} events --event-type {EventType} --event-type {EventType2} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyPolicyContainsTopicFor(EndpointName, prefix, EventType);
            await VerifyPolicyContainsTopicFor(EndpointName, prefix, EventType2);
        }

        [Test]
        public async Task Set_policy_account_wildcard()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} wildcard --account-condition --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyPolicyContainsAccountWildCard(EndpointName, prefix);
        }

        [Test]
        public async Task Set_policy_prefix_wildcard()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} wildcard --prefix-condition --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyPolicyContainsPrefixWildCard(EndpointName, prefix);
        }

        [Test]
        public async Task Set_policy_namespace_wildcard()
        {
            var ns = "MyNamespace";

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} wildcard --namespace-condition {ns} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyPolicyContainsNamespaceWildCard(EndpointName, prefix, ns);
        }

        [Test]
        public async Task Remove_multiple_events_when_setting_wildcard_policy()
        {
            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType2} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} events --event-type {EventType} --event-type {EventType2} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            (_, error, exitCode) = await Execute($"endpoint set-policy {EndpointName} wildcard --account-condition --remove-event-type {EventType} --remove-event-type {EventType2} --prefix {prefix}");

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(error == string.Empty);

            await VerifyPolicyDoesNotContainTopicFor(EndpointName, prefix, EventType);
            await VerifyPolicyDoesNotContainTopicFor(EndpointName, prefix, EventType2);
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

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            await VerifyBucket(bucketName);

            (_, error, exitCode) = await Execute($"endpoint remove {EndpointName} large-message-support {bucketName}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            await VerifyBucket(bucketName);
        }

        [Test]
        public async Task Remove_large_message_support_from_endpoint_removes_bucket_with_remove_shared_resources()
        {
            var bucketName = prefix + BucketName;

            var (_, error, exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

            await VerifyBucket(bucketName);

            (_, error, exitCode) = await Execute($"endpoint remove {EndpointName} large-message-support {bucketName} --remove-shared-resources");

            Assert.IsTrue(error == string.Empty);
            Assert.AreEqual(0, exitCode);

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
            var process = new Process
            {
                StartInfo =
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = TestContext.CurrentContext.TestDirectory,
                    FileName = "dotnet",
                    Arguments =
                        $"NServiceBus.Transports.SQS.CommandLine.dll {command} -i {accessKeyId} -s {secretAccessKey} -r {region}"
                }
            };

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
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;
            retentionPeriodInSeconds ??= DefaultConfigurationValues.RetentionPeriod.TotalSeconds;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);

            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string> { QueueAttributeName.MessageRetentionPeriod, QueueAttributeName.QueueArn }).ConfigureAwait(false);

            Assert.AreEqual(retentionPeriodInSeconds, queueAttributesResponse.MessageRetentionPeriod);

            return queueAttributesResponse.QueueARN;
        }

        async Task<string> VerifyDelayDeliveryQueue(string queueName, string prefix = null, double? retentionPeriodInSeconds = null, double? delayInSeconds = null, string suffix = null)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;
            retentionPeriodInSeconds ??= DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
            delayInSeconds ??= DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds;
            suffix ??= DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

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
            prefix ??= DefaultConfigurationValues.TopicNamePrefix;

            var topicName = TopicSanitization.GetSanitizedTopicName($"{prefix}{eventType}");

            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);

            Assert.IsNotNull(findTopicResponse.TopicArn);

            return findTopicResponse.TopicArn;
        }

        async Task VerifyPolicyContainsTopicFor(string queueName, string prefix, string eventType)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string>
            {
                QueueAttributeName.Policy
            }).ConfigureAwait(false);
            var policy = Policy.FromJson(queueAttributesResponse.Policy);

            var topicName = TopicSanitization.GetSanitizedTopicName($"{prefix}{eventType}");
            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);

            Assert.IsTrue(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(findTopicResponse.TopicArn))));
        }

        async Task VerifyPolicyDoesNotContainTopicFor(string queueName, string prefix, string eventType)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string>
            {
                QueueAttributeName.Policy
            }).ConfigureAwait(false);
            var policy = Policy.FromJson(queueAttributesResponse.Policy);

            var topicName = TopicSanitization.GetSanitizedTopicName($"{prefix}{eventType}");
            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);

            Assert.IsFalse(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(findTopicResponse.TopicArn))));
        }

        async Task VerifyPolicyContainsAccountWildCard(string queueName, string prefix)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string>
            {
                QueueAttributeName.QueueArn,
                QueueAttributeName.Policy
            }).ConfigureAwait(false);
            var policy = Policy.FromJson(queueAttributesResponse.Policy);

            var parts = queueAttributesResponse.QueueARN.Split(":", StringSplitOptions.RemoveEmptyEntries);
            var accountArn = $"{parts[0]}:{parts[1]}:sns:{parts[3]}:{parts[4]}:*";

            Assert.IsTrue(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(accountArn))));
        }

        async Task VerifyPolicyContainsPrefixWildCard(string queueName, string prefix)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string>
            {
                QueueAttributeName.QueueArn,
                QueueAttributeName.Policy
            }).ConfigureAwait(false);
            var policy = Policy.FromJson(queueAttributesResponse.Policy);

            var parts = queueAttributesResponse.QueueARN.Split(":", StringSplitOptions.RemoveEmptyEntries);
            var prefixArn = $"{parts[0]}:{parts[1]}:sns:{parts[3]}:{parts[4]}:{prefix}*";

            Assert.IsTrue(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(prefixArn))));
        }

        async Task VerifyPolicyContainsNamespaceWildCard(string queueName, string prefix, string ns)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            var queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
            var queueAttributesResponse = await sqs.GetQueueAttributesAsync(queueUrlResponse.QueueUrl, new List<string>
            {
                QueueAttributeName.QueueArn,
                QueueAttributeName.Policy
            }).ConfigureAwait(false);
            var policy = Policy.FromJson(queueAttributesResponse.Policy);

            var parts = queueAttributesResponse.QueueARN.Split(":", StringSplitOptions.RemoveEmptyEntries);
            var namespaceArn = $"{parts[0]}:{parts[1]}:sns:{parts[3]}:{parts[4]}:{GetNamespaceName(prefix, ns)}*";

            Assert.IsTrue(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(namespaceArn))));
        }

        static string GetNamespaceName(string topicNamePrefix, string namespaceName)
        {
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            var namespaceNameBuilder = new StringBuilder(namespaceName);
            for (var i = 0; i < namespaceNameBuilder.Length; ++i)
            {
                var c = namespaceNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    namespaceNameBuilder[i] = '-';
                }
            }

            // topicNamePrefix should not be sanitized
            return topicNamePrefix + namespaceNameBuilder;
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
            keyPrefix ??= DefaultConfigurationValues.S3KeyPrefix;
            expiration ??= (int)Math.Ceiling(DefaultConfigurationValues.RetentionPeriod.TotalDays);

            LifecycleRule setLifeCycleConfig;
            int backOff;
            var executions = 0;
            do
            {
                backOff = executions * executions * VerificationBackoffInterval;
                await Task.Delay(backOff);
                executions++;

                var lifecycleConfig = await s3.GetLifecycleConfigurationAsync(bucketName).ConfigureAwait(false);
                setLifeCycleConfig = lifecycleConfig.Configuration.Rules.FirstOrDefault(x => x.Id == "NServiceBus.SQS.DeleteMessageBodies");
            }
            while (setLifeCycleConfig == null && backOff < MaximumBackoffInterval);

            Assert.IsNotNull(setLifeCycleConfig);
            Assert.AreEqual(expiration, setLifeCycleConfig.Expiration.Days);
            Assert.AreEqual(keyPrefix, (setLifeCycleConfig.Filter.LifecycleFilterPredicate as LifecyclePrefixPredicate).Prefix);
        }

        async Task VerifySubscription(string topicArn, string queueArn)
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
            }
            while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            Assert.IsNotNull(subscription);
        }

        async Task VerifySubscriptionDeleted(string topicArn, string queueArn)
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
            }
            while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            Assert.IsNull(subscription);
        }

        async Task VerifyTopicDeleted(string eventType, string prefix = null)
        {
            prefix ??= DefaultConfigurationValues.TopicNamePrefix;

            var topicName = TopicSanitization.GetSanitizedTopicName($"{prefix}{eventType}");

            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);

            Assert.IsNull(findTopicResponse);
        }

        async Task VerifyDelayDeliveryQueueDeleted(string queueName, string prefix = null, string suffix = null)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;
            suffix ??= DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}{suffix}");
            GetQueueUrlResponse queueUrlResponse;
            var backOff = 0;
            var executions = 0;
            do
            {
                try
                {
                    backOff = executions * executions * VerificationBackoffInterval;
                    await Task.Delay(backOff);
                    executions++;

                    queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                }
                catch (QueueDoesNotExistException)
                {
                    // expected
                    queueUrlResponse = null;
                }
            }
            while (queueUrlResponse != null && backOff < MaximumBackoffInterval);

            Assert.IsNull(queueUrlResponse);
        }

        async Task VerifyQueueDeleted(string queueName, string prefix = null)
        {
            prefix ??= DefaultConfigurationValues.QueueNamePrefix;

            var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
            GetQueueUrlResponse queueUrlResponse;
            var backOff = 0;
            var executions = 0;
            do
            {
                try
                {
                    backOff = executions * executions * VerificationBackoffInterval;
                    await Task.Delay(backOff);
                    executions++;

                    queueUrlResponse = await sqs.GetQueueUrlAsync(getQueueUrlRequest).ConfigureAwait(false);
                }
                catch (QueueDoesNotExistException)
                {
                    // expected
                    queueUrlResponse = null;
                }
            }
            while (queueUrlResponse != null && backOff < MaximumBackoffInterval);

            Assert.IsNull(queueUrlResponse);
        }

        async Task VerifyBucketDeleted(string bucketName)
        {
            int backOff;
            var executions = 0;
            bool bucketExists;
            do
            {
                backOff = executions * executions * VerificationBackoffInterval;
                await Task.Delay(backOff);
                executions++;

                bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(s3, bucketName);
            }
            while (bucketExists && backOff < MaximumBackoffInterval);

            Assert.IsFalse(bucketExists);
        }

        [SetUp]
        public void Setup()
        {
            prefix = $"cli-{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "").ToLowerInvariant()}-";

            var regionEndpoint = RegionEndpoint.GetBySystemName(region);

            sqs = new AmazonSQSClient(accessKeyId, secretAccessKey, regionEndpoint);
            sns = new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey, regionEndpoint);
            s3 = new AmazonS3Client(accessKeyId, secretAccessKey, regionEndpoint);
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

        readonly string accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        readonly string secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        readonly string region = Environment.GetEnvironmentVariable("AWS_REGION");

        IAmazonSQS sqs;
        IAmazonSimpleNotificationService sns;
        IAmazonS3 s3;
        const string EndpointName = "nsb-cli-test";
        const string BucketName = "nsb-cli-test-bucket";
        const string EventType = "MyNamespace.MyMessage1";
        const string EventType2 = "MyNamespace.MyMessage2";
        const int VerificationBackoffInterval = 200;
        const int MaximumBackoffInterval = 60000;
    }
}