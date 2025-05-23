namespace NServiceBus.Transport.SQS.CommandLine.Tests;

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
    [SetUp]
    public void Setup()
    {
        prefix =
            $"cli-{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "").ToLowerInvariant()}-";

        var regionEndpoint = RegionEndpoint.GetBySystemName(region);

        sqsClient = new AmazonSQSClient(accessKeyId, secretAccessKey, regionEndpoint);
        snsClient = new AmazonSimpleNotificationServiceClient(accessKeyId, secretAccessKey, regionEndpoint);
        s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, regionEndpoint);
    }

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, s3Client, prefix);
        }
        finally
        {
            sqsClient?.Dispose();
            snsClient?.Dispose();
            s3Client?.Dispose();
        }
    }

    [Test]
    public async Task Create_endpoint_without_prefix_when_there_are_no_entities()
    {
        string endpointName = prefix + EndpointName;

        (_, string error, int exitCode) = await Execute($"endpoint create {endpointName}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyQueue(endpointName);
    }

    [Test]
    public async Task Create_endpoint_without_prefix_when_there_are_entities()
    {
        string endpointName = prefix + EndpointName;

        await Execute($"endpoint create {endpointName}");
        (_, string error, int exitCode) = await Execute($"endpoint create {endpointName}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyQueue(endpointName);
    }

    [Test]
    public async Task Create_endpoint()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyQueue(EndpointName, prefix);
    }

    [Test]
    public async Task Create_endpoint_with_custom_retention()
    {
        const int customRetention = 60000;
        (_, string error, int exitCode) =
            await Execute($"endpoint create {EndpointName} --retention {customRetention} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyQueue(EndpointName, prefix, retentionPeriodInSeconds: customRetention);
    }

    [Test]
    public async Task Enable_large_message_support_on_endpoint()
    {
        string bucketName = prefix + BucketName;

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucket(bucketName);
    }

    [Test]
    public async Task Enable_large_message_support_on_endpoint_with_custom_key_prefix()
    {
        string bucketName = prefix + BucketName;

        string keyPrefix = "k-";

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        (_, error, exitCode) =
            await Execute($"endpoint add {EndpointName} large-message-support {bucketName} --key-prefix {keyPrefix}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucket(bucketName);
        await VerifyLifecycleConfiguration(bucketName, keyPrefix);
    }

    [Test]
    public async Task Enable_large_message_support_on_endpoint_with_custom_expiration()
    {
        string bucketName = prefix + BucketName;

        int expiration = 7;

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        (_, error, exitCode) =
            await Execute($"endpoint add {EndpointName} large-message-support {bucketName} --expiration {expiration}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucket(bucketName);
        await VerifyLifecycleConfiguration(bucketName, expiration: expiration);
    }

    [Test]
    public async Task Enable_delay_delivery_on_endpoint()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyDelayDeliveryQueue(EndpointName, prefix);
    }

    [Test]
    public async Task Enable_delay_delivery_on_endpoint_with_custom_retention()
    {
        const int customRetention = 60000;

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute(
                $"endpoint add {EndpointName} delay-delivery-support --retention {customRetention} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyDelayDeliveryQueue(EndpointName, prefix, retentionPeriodInSeconds: customRetention);
    }

    [Test]
    public async Task Subscribe_on_event()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        string queueArn = await VerifyQueue(EndpointName, prefix);
        string topicArn = await VerifyTopic(EventType, prefix);
        await VerifySubscription(topicArn, queueArn);
    }

    [Test]
    public async Task Unsubscribe_from_event()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        string queueArn = await VerifyQueue(EndpointName, prefix);
        string topicArn = await VerifyTopic(EventType, prefix);
        await VerifySubscription(topicArn, queueArn);

        await Execute($"endpoint unsubscribe {EndpointName} {EventType} --prefix {prefix}");

        await VerifySubscriptionDeleted(topicArn, queueArn);
        await VerifyTopic(EventType, prefix);
    }

    [Test]
    public async Task Unsubscribe_from_event_without_topic_warns()
    {
        (string output, string error, int exitCode) =
            await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(output, Is.Not.Null);
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (output, error, exitCode) = await Execute($"endpoint unsubscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));

            Assert.That(output,
                Does.Contain(
                    $"No topic detected for event type '{EventType}', please subscribe to the event type first."));
        });
    }

    [Test]
    public async Task List_policy()
    {
        (string output, string error, int exitCode) =
            await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(output, Is.Not.Null);
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (output, error, exitCode) = await Execute($"endpoint list-policy {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));

            Assert.That(output, Is.Not.Null);
        });
        Assert.That(output, Does.Contain("Statement"));
    }

    [Test]
    public async Task Set_policy_single_event()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute($"endpoint set-policy {EndpointName} events --event-type {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyPolicyContainsTopicFor(EndpointName, prefix, EventType);
    }

    [Test]
    public async Task Set_policy_single_event_without_subscribe_warns()
    {
        (string output, string error, int exitCode) =
            await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(output, Is.Not.Null);
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (output, error, exitCode) =
            await Execute($"endpoint set-policy {EndpointName} events --event-type {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        Assert.That(output,
            Does.Contain($"No topic detected for event type '{EventType}', please subscribe to the event type first."));
    }

    [Test]
    public async Task Set_policy_multiple_events()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType2} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute(
                $"endpoint set-policy {EndpointName} events --event-type {EventType} --event-type {EventType2} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyPolicyContainsTopicFor(EndpointName, prefix, EventType);
        await VerifyPolicyContainsTopicFor(EndpointName, prefix, EventType2);
    }

    [Test]
    public async Task Set_policy_account_wildcard()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute($"endpoint set-policy {EndpointName} wildcard --account-condition --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyPolicyContainsAccountWildCard(EndpointName, prefix);
    }

    [Test]
    public async Task Set_policy_prefix_wildcard()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute($"endpoint set-policy {EndpointName} wildcard --prefix-condition --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyPolicyContainsPrefixWildCard(EndpointName, prefix);
    }

    [Test]
    public async Task Set_policy_namespace_wildcard()
    {
        string ns = "MyNamespace";

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute($"endpoint set-policy {EndpointName} wildcard --namespace-condition {ns} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyPolicyContainsNamespaceWildCard(EndpointName, prefix, ns);
    }

    [Test]
    public async Task Remove_multiple_events_when_setting_wildcard_policy()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType2} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute(
                $"endpoint set-policy {EndpointName} events --event-type {EventType} --event-type {EventType2} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) =
            await Execute(
                $"endpoint set-policy {EndpointName} wildcard --account-condition --remove-event-type {EventType} --remove-event-type {EventType2} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyPolicyDoesNotContainTopicFor(EndpointName, prefix, EventType);
        await VerifyPolicyDoesNotContainTopicFor(EndpointName, prefix, EventType2);
    }

    [Test]
    public async Task Unsubscribe_from_event_with_remove_shared_resources()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint subscribe {EndpointName} {EventType} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        string queueArn = await VerifyQueue(EndpointName, prefix);
        string topicArn = await VerifyTopic(EventType, prefix);
        await VerifySubscription(topicArn, queueArn);

        await Execute($"endpoint unsubscribe {EndpointName} {EventType} --prefix {prefix} --remove-shared-resources");

        await VerifyTopicDeleted(EventType, prefix);
    }

    [Test]
    public async Task Remove_delay_delivery_from_endpoint()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        (_, error, exitCode) = await Execute($"endpoint add {EndpointName} delay-delivery-support --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyDelayDeliveryQueue(EndpointName, prefix);

        (_, error, exitCode) =
            await Execute($"endpoint remove {EndpointName} delay-delivery-support --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyDelayDeliveryQueueDeleted(EndpointName, prefix);
    }

    [Test]
    public async Task Remove_large_message_support_from_endpoint_does_not_remove_bucket_by_default()
    {
        string bucketName = prefix + BucketName;

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucket(bucketName);

        (_, error, exitCode) = await Execute($"endpoint remove {EndpointName} large-message-support {bucketName}");

        Assert.Multiple(() =>
        {
            Assert.That(error == string.Empty, Is.True);
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucket(bucketName);
    }

    [Test]
    public async Task Remove_large_message_support_from_endpoint_removes_bucket_with_remove_shared_resources()
    {
        string bucketName = prefix + BucketName;

        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        (_, error, exitCode) = await Execute($"endpoint add {EndpointName} large-message-support {bucketName}");

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.EqualTo(string.Empty));
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucket(bucketName);

        (_, error, exitCode) =
            await Execute(
                $"endpoint remove {EndpointName} large-message-support {bucketName} --remove-shared-resources");

        Assert.Multiple(() =>
        {
            Assert.That(error == string.Empty, Is.True);
            Assert.That(exitCode, Is.EqualTo(0));
        });

        await VerifyBucketDeleted(bucketName);
    }

    [Test]
    public async Task Delete_endpoint()
    {
        (_, string error, int exitCode) = await Execute($"endpoint create {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyQueue(EndpointName, prefix);

        (_, error, exitCode) = await Execute($"endpoint delete {EndpointName} --prefix {prefix}");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(error, Is.EqualTo(string.Empty));
        });

        await VerifyQueueDeleted(EndpointName, prefix);
    }

    async Task<(string output, string error, int exitCode)> Execute(string command)
    {
        var frameworkVersion = RuntimeInformation.FrameworkDescription.AsSpan()[5..];

        var process = new Process
        {
            StartInfo =
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = TestContext.CurrentContext.TestDirectory,
                FileName = "dotnet",
                Arguments =
                    $"--fx-version {frameworkVersion} NServiceBus.Transports.SQS.CommandLine.dll {command} -i {accessKeyId} -s {secretAccessKey} -r {region}"
            }
        };

        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit(10000);

        string output = await outputTask;
        string error = await errorTask;

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

    async Task<string> VerifyQueue(string queueName, string queuePrefix = null, int? retentionPeriodInSeconds = null)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;
        retentionPeriodInSeconds ??= (int)DefaultConfigurationValues.RetentionPeriod.TotalSeconds;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);

        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl, [QueueAttributeName.MessageRetentionPeriod, QueueAttributeName.QueueArn]);

        Assert.That(queueAttributesResponse.MessageRetentionPeriod, Is.EqualTo(retentionPeriodInSeconds));

        return queueAttributesResponse.QueueARN;
    }

    async Task<string> VerifyDelayDeliveryQueue(string queueName, string queuePrefix = null,
        int? retentionPeriodInSeconds = null, int? delayInSeconds = null, string suffix = null)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;
        retentionPeriodInSeconds ??= (int)DefaultConfigurationValues.RetentionPeriod.TotalSeconds;
        delayInSeconds ??= (int)DefaultConfigurationValues.MaximumQueueDelayTime.TotalSeconds;
        suffix ??= DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}{suffix}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);

        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl,
            [
                QueueAttributeName.MessageRetentionPeriod,
                QueueAttributeName.DelaySeconds,
                QueueAttributeName.QueueArn
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(queueAttributesResponse.MessageRetentionPeriod, Is.EqualTo(retentionPeriodInSeconds));
            Assert.That(queueAttributesResponse.DelaySeconds, Is.EqualTo(delayInSeconds));
        });

        return queueAttributesResponse.QueueARN;
    }

    async Task<string> VerifyTopic(string eventType, string topicPrefix = null)
    {
        topicPrefix ??= DefaultConfigurationValues.TopicNamePrefix;

        string topicName = TopicSanitization.GetSanitizedTopicName($"{topicPrefix}{eventType}");

        Topic findTopicResponse = await snsClient.FindTopicAsync(topicName);

        Assert.That(findTopicResponse.TopicArn, Is.Not.Null);

        return findTopicResponse.TopicArn;
    }

    async Task VerifyPolicyContainsTopicFor(string queueName, string queuePrefix, string eventType)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl,
            [
                QueueAttributeName.Policy
            ]);
        var policy = Policy.FromJson(queueAttributesResponse.Policy);

        string topicName = TopicSanitization.GetSanitizedTopicName($"{queuePrefix}{eventType}");
        Topic findTopicResponse = await snsClient.FindTopicAsync(topicName);

        Assert.That(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(findTopicResponse.TopicArn))),
            Is.True);
    }

    async Task VerifyPolicyDoesNotContainTopicFor(string queueName, string queuePrefix, string eventType)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl,
            [
                QueueAttributeName.Policy
            ]);
        var policy = Policy.FromJson(queueAttributesResponse.Policy);

        string topicName = TopicSanitization.GetSanitizedTopicName($"{queuePrefix}{eventType}");
        Topic findTopicResponse = await snsClient.FindTopicAsync(topicName);

        Assert.That(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(findTopicResponse.TopicArn))),
            Is.False);
    }

    async Task VerifyPolicyContainsAccountWildCard(string queueName, string queuePrefix)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl,
            [
                QueueAttributeName.QueueArn,
                QueueAttributeName.Policy
            ]);
        var policy = Policy.FromJson(queueAttributesResponse.Policy);

        string[] parts = queueAttributesResponse.QueueARN.Split(":", StringSplitOptions.RemoveEmptyEntries);
        string accountArn = $"{parts[0]}:{parts[1]}:sns:{parts[3]}:{parts[4]}:*";

        Assert.That(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(accountArn))), Is.True);
    }

    async Task VerifyPolicyContainsPrefixWildCard(string queueName, string queuePrefix)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl,
            [
                QueueAttributeName.QueueArn,
                QueueAttributeName.Policy
            ]);
        var policy = Policy.FromJson(queueAttributesResponse.Policy);

        string[] parts = queueAttributesResponse.QueueARN.Split(":", StringSplitOptions.RemoveEmptyEntries);
        string prefixArn = $"{parts[0]}:{parts[1]}:sns:{parts[3]}:{parts[4]}:{queuePrefix}*";

        Assert.That(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(prefixArn))), Is.True);
    }

    async Task VerifyPolicyContainsNamespaceWildCard(string queueName, string queuePrefix, string ns)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
        GetQueueAttributesResponse queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(
            queueUrlResponse.QueueUrl,
            [
                QueueAttributeName.QueueArn,
                QueueAttributeName.Policy
            ]);
        var policy = Policy.FromJson(queueAttributesResponse.Policy);

        string[] parts = queueAttributesResponse.QueueARN.Split(":", StringSplitOptions.RemoveEmptyEntries);
        string namespaceArn = $"{parts[0]}:{parts[1]}:sns:{parts[3]}:{parts[4]}:{GetNamespaceName(queuePrefix, ns)}*";

        Assert.That(policy.Statements.Any(s => s.Conditions.Any(c => c.Values.Contains(namespaceArn))), Is.True);
    }

    static string GetNamespaceName(string topicNamePrefix, string namespaceName)
    {
        // SNS topic names can only have alphanumeric characters, hyphens and underscores.
        // Any other characters will be replaced with a hyphen.
        var namespaceNameBuilder = new StringBuilder(namespaceName);
        for (int i = 0; i < namespaceNameBuilder.Length; ++i)
        {
            char c = namespaceNameBuilder[i];
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
        ListBucketsResponse listBucketsResponse = await s3Client.ListBucketsAsync(new ListBucketsRequest());
        S3Bucket bucket = listBucketsResponse.Buckets.FirstOrDefault(x =>
            string.Equals(x.BucketName, bucketName, StringComparison.InvariantCultureIgnoreCase));

        Assert.That(bucket, Is.Not.Null);

        return bucket.BucketName;
    }

    async Task VerifyLifecycleConfiguration(string bucketName, string keyPrefix = null, int? expiration = null)
    {
        keyPrefix ??= DefaultConfigurationValues.S3KeyPrefix;
        expiration ??= (int)Math.Ceiling(DefaultConfigurationValues.RetentionPeriod.TotalDays);

        LifecycleRule setLifeCycleConfig;
        int backOff;
        int executions = 0;
        do
        {
            backOff = executions * executions * VerificationBackoffInterval;
            await Task.Delay(backOff);
            executions++;

            GetLifecycleConfigurationResponse lifecycleConfig =
                await s3Client.GetLifecycleConfigurationAsync(bucketName);
            setLifeCycleConfig =
                lifecycleConfig.Configuration.Rules?.FirstOrDefault(x => x.Id == "NServiceBus.SQS.DeleteMessageBodies");
        } while (setLifeCycleConfig == null && backOff < MaximumBackoffInterval);

        Assert.That(setLifeCycleConfig, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(setLifeCycleConfig.Expiration.Days, Is.EqualTo(expiration));
            Assert.That(((LifecyclePrefixPredicate)setLifeCycleConfig.Filter.LifecycleFilterPredicate).Prefix,
                Is.EqualTo(keyPrefix));
        });
    }

    async Task VerifySubscription(string topicArn, string queueArn)
    {
        ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;
        Subscription subscription = null;

        do
        {
            upToAHundredSubscriptions =
                await snsClient.ListSubscriptionsByTopicAsync(topicArn, upToAHundredSubscriptions?.NextToken);

            foreach (Subscription upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions ?? Enumerable.Empty<Subscription>())
            {
                if (upToAHundredSubscription.Endpoint == queueArn)
                {
                    subscription = upToAHundredSubscription;
                }
            }
        } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions is { Count: > 0 });

        Assert.That(subscription, Is.Not.Null);
    }

    async Task VerifySubscriptionDeleted(string topicArn, string queueArn)
    {
        ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;
        Subscription subscription = null;

        do
        {
            upToAHundredSubscriptions =
                await snsClient.ListSubscriptionsByTopicAsync(topicArn, upToAHundredSubscriptions?.NextToken);

            foreach (Subscription upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions ?? Enumerable.Empty<Subscription>())
            {
                if (upToAHundredSubscription.Endpoint == queueArn)
                {
                    subscription = upToAHundredSubscription;
                }
            }
        } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions is { Count: > 0 });

        Assert.That(subscription, Is.Null);
    }

    async Task VerifyTopicDeleted(string eventType, string topicPrefix = null)
    {
        topicPrefix ??= DefaultConfigurationValues.TopicNamePrefix;

        string topicName = TopicSanitization.GetSanitizedTopicName($"{topicPrefix}{eventType}");

        Topic findTopicResponse = await snsClient.FindTopicAsync(topicName);

        Assert.That(findTopicResponse, Is.Null);
    }

    async Task VerifyDelayDeliveryQueueDeleted(string queueName, string queuePrefix = null, string suffix = null)
    {
        queuePrefix ??= DefaultConfigurationValues.QueueNamePrefix;
        suffix ??= DefaultConfigurationValues.DelayedDeliveryQueueSuffix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{queuePrefix}{queueName}{suffix}");
        GetQueueUrlResponse queueUrlResponse;
        int backOff = 0;
        int executions = 0;
        do
        {
            try
            {
                backOff = executions * executions * VerificationBackoffInterval;
                await Task.Delay(backOff);
                executions++;

                queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
            }
            catch (QueueDoesNotExistException)
            {
                // expected
                queueUrlResponse = null;
            }
        } while (queueUrlResponse != null && backOff < MaximumBackoffInterval);

        Assert.That(queueUrlResponse, Is.Null);
    }

    async Task VerifyQueueDeleted(string queueName, string prefix = null)
    {
        prefix ??= DefaultConfigurationValues.QueueNamePrefix;

        var getQueueUrlRequest = new GetQueueUrlRequest($"{prefix}{queueName}");
        GetQueueUrlResponse queueUrlResponse;
        int backOff = 0;
        int executions = 0;
        do
        {
            try
            {
                backOff = executions * executions * VerificationBackoffInterval;
                await Task.Delay(backOff);
                executions++;

                queueUrlResponse = await sqsClient.GetQueueUrlAsync(getQueueUrlRequest);
            }
            catch (QueueDoesNotExistException)
            {
                // expected
                queueUrlResponse = null;
            }
        } while (queueUrlResponse != null && backOff < MaximumBackoffInterval);

        Assert.That(queueUrlResponse, Is.Null);
    }

    async Task VerifyBucketDeleted(string bucketName)
    {
        int backOff;
        int executions = 0;
        bool bucketExists;
        do
        {
            backOff = executions * executions * VerificationBackoffInterval;
            await Task.Delay(backOff);
            executions++;

            bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
        } while (bucketExists && backOff < MaximumBackoffInterval);

        Assert.That(bucketExists, Is.False);
    }

    string prefix;

    readonly string accessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    readonly string secretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
    readonly string region = Environment.GetEnvironmentVariable("AWS_REGION");

    IAmazonSQS sqsClient;
    IAmazonSimpleNotificationService snsClient;
    IAmazonS3 s3Client;
    const string EndpointName = "nsb-cli-test";
    const string BucketName = "nsb-cli-test-bucket";
    const string EventType = "MyNamespace.MyMessage1";
    const string EventType2 = "MyNamespace.MyMessage2";
    const int VerificationBackoffInterval = 200;
    const int MaximumBackoffInterval = 60000;
}