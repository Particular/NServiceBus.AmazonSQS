using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus;
using NUnit.Framework;
using System;
using NServiceBus.Settings;

[TestFixture]
public class Configuring_transport
{
    [Test]
    public void Parsing_valid_region_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.Region("ap-southeast-2");

        Assert.AreEqual(Amazon.RegionEndpoint.APSoutheast2, result.GetSettings().Get("NServiceBus.AmazonSQS.Region"));
    }

    [Test]
    public void Invalid_region_throws()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        Assert.Throws<ArgumentException>(() => extensions.Region("not-a-valid-region"));
    }


    [Test]
    public void Parsing_s3_bucket_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
        var result = extensions.S3BucketForLargeMessages("myTestBucket", "blah\blah");

        Assert.AreEqual("myTestBucket", result.GetSettings().Get("NServiceBus.AmazonSQS.S3BucketForLargeMessages"));
    }

    [Test]
    public void Throws_if_s3_bucket_is_specified_without_key_prefix()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        Assert.Throws<ArgumentNullException>(() => extensions.S3BucketForLargeMessages("myTestBucket", string.Empty));
    }

    [Test]
    public void Parsing_max_ttl_days_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.MaxTtl(TimeSpan.FromDays(1));

        Assert.AreEqual(TimeSpan.FromDays(1), result.GetSettings().Get(SettingsKeys.MaxTtl));
    }

    [Test]
    public void Invalid_max_ttl_days_throws()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        Assert.Throws<ArgumentException>(() => extensions.MaxTtl(TimeSpan.FromDays(100)));
    }

    [Test]
    public void Parsing_queue_name_prefix_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.QueueNamePrefix("DEV");

        Assert.AreEqual("DEV", result.GetSettings().Get("NServiceBus.AmazonSQS.QueueNamePrefix"));
    }

    [Test]
    public void Parsing_instance_profile_credential_source_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.CredentialSource(SqsCredentialSource.InstanceProfile);

        Assert.AreEqual(SqsCredentialSource.InstanceProfile, result.GetSettings().Get("NServiceBus.AmazonSQS.CredentialSource"));
    }

    [Test]
    public void Parsing_environment_variables_credential_source_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.CredentialSource(SqsCredentialSource.EnvironmentVariables);

        Assert.AreEqual(SqsCredentialSource.EnvironmentVariables, result.GetSettings().Get("NServiceBus.AmazonSQS.CredentialSource"));
    }

    [Test]
    public void Parsing_proxy_host_and_port_works()
    {
        var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

        var result = extensions.Proxy("localhost", 8080);

        Assert.AreEqual("localhost", result.GetSettings().Get("NServiceBus.AmazonSQS.ProxyHost"));
        Assert.AreEqual(8080, result.GetSettings().Get("NServiceBus.AmazonSQS.ProxyPort"));
    }
}