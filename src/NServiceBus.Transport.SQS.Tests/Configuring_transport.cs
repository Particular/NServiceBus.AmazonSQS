namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using Amazon.S3;
    using Configuration.AdvancedExtensibility;
    using Configure;
    using NServiceBus;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class Configuring_transport
    {
        [Test]
        public void Parsing_s3_bucket_works()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
            var result = extensions.S3("myTestBucket", "blah\blah");

            Assert.AreEqual("myTestBucket", result.GetSettings().Get("NServiceBus.AmazonSQS.S3BucketForLargeMessages"));
        }

        [Test]
        public void Throws_if_s3_bucket_is_specified_without_key_prefix()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());

            Assert.Throws<ArgumentNullException>(() => extensions.S3("myTestBucket", string.Empty));
        }

        [Test]
        public void Parsing_s3_server_side_settings_works()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
            var s3Settings = extensions.S3("myTestBucket", "blah\blah");
            s3Settings.ServerSideEncryption(ServerSideEncryptionMethod.AES256, "SomeKey");

            Assert.AreEqual(ServerSideEncryptionMethod.AES256, s3Settings.GetSettings().Get("NServiceBus.AmazonSQS.ServerSideEncryptionMethod"));
            Assert.AreEqual("SomeKey", s3Settings.GetSettings().Get("NServiceBus.AmazonSQS.ServerSideEncryptionKeyManagementServiceKeyId"));
        }

        [Test]
        public void Parsing_s3_server_side_customer_settings_works()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
            var s3Settings = extensions.S3("myTestBucket", "blah\blah");
            s3Settings.ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod.AES256, "SomeKey", "SomeMD5");

            Assert.AreEqual(ServerSideEncryptionCustomerMethod.AES256, s3Settings.GetSettings().Get("NServiceBus.AmazonSQS.ServerSideEncryptionCustomerMethod"));
            Assert.AreEqual("SomeKey", s3Settings.GetSettings().Get("NServiceBus.AmazonSQS.ServerSideEncryptionCustomerProvidedKey"));
            Assert.AreEqual("SomeMD5", s3Settings.GetSettings().Get("NServiceBus.AmazonSQS.ServerSideEncryptionCustomerProvidedKeyMD5"));
        }

        [Test]
        public void S3_server_side_customer_settings_provided_key_not_empty()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
            var s3Settings = extensions.S3("myTestBucket", "blah\blah");

            Assert.Throws<ArgumentException>(() => s3Settings.ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod.AES256, ""));
        }

        [Test]
        public void S3_server_side_settings_cannot_be_used_with_customer_settings()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
            var s3Settings = extensions.S3("myTestBucket", "blah\blah");
            s3Settings.ServerSideEncryption(ServerSideEncryptionMethod.AES256, "SomeKey");

            Assert.Throws<InvalidOperationException>(() => s3Settings.ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod.AES256, "SomeKey"));
        }

        [Test]
        public void S3_customer_settings_cannot_be_used_with_server_side_settings()
        {
            var extensions = new TransportExtensions<SqsTransport>(new SettingsHolder());
            var s3Settings = extensions.S3("myTestBucket", "blah\blah");
            s3Settings.ServerSideCustomerEncryption(ServerSideEncryptionCustomerMethod.AES256, "SomeKey");

            Assert.Throws<InvalidOperationException>(() => s3Settings.ServerSideEncryption(ServerSideEncryptionMethod.AES256, "SomeKey"));
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
}