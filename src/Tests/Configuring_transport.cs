using NServiceBus;
using NUnit.Framework;
using System;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Settings;

[TestFixture]
public class Configuring_transport
{
    [Test]
    public void Parsing_s3_bucket_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
        var result = extensions.S3().BucketForLargeMessages("myTestBucket", "blah\blah");

        Assert.AreEqual("myTestBucket", result.GetSettings().Get("NServiceBus.AmazonSQS.S3BucketForLargeMessages"));
    }

    [Test]
    public void Throws_if_s3_bucket_is_specified_without_key_prefix()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        Assert.Throws<ArgumentNullException>(() => extensions.S3().BucketForLargeMessages("myTestBucket", string.Empty));
    }

    [Test]
    public void Parsing_max_ttl_days_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.MaxTimeToLive(TimeSpan.FromDays(1));

        Assert.AreEqual(TimeSpan.FromDays(1), result.GetSettings().Get(SettingsKeys.MaxTimeToLive));
    }

    [Test]
    public void Invalid_max_ttl_days_throws()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        Assert.Throws<ArgumentException>(() => extensions.MaxTimeToLive(TimeSpan.FromDays(100)));
    }

    [Test]
    public void Parsing_queue_name_prefix_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.QueueNamePrefix("DEV");

        Assert.AreEqual("DEV", result.GetSettings().Get("NServiceBus.AmazonSQS.QueueNamePrefix"));
    }
}