using NServiceBus.Configuration.AdvanceExtensibility;
using NUnit.Framework;
using System;

namespace NServiceBus.AmazonSQS.Tests
{
    [TestFixture]
    public class when_configuring_transport
    {
        TransportExtensions<SqsTransport> SUT()
        {
            return new TransportExtensions<SqsTransport>(new Settings.SettingsHolder());
        }

        [Test]
        public void parsing_valid_region_works()
        {
            var sut = SUT();

            var result = sut.Region("ap-southeast-2");

            Assert.AreEqual(Amazon.RegionEndpoint.APSoutheast2, result.GetSettings().Get("NServiceBus.AmazonSQS.Region"));
        }
        
        [Test]
        public void invalid_region_throws()
        {
            var sut = SUT();

            Assert.Throws<ArgumentException>(() => sut.Region("not-a-valid-region"));
        }

        
        [Test]
        public void parsing_s3_bucket_works()
        {
            var sut = SUT();
            var result = sut.S3BucketForLargeMessages("myTestBucket", "blah\blah");
            
            Assert.AreEqual("myTestBucket", result.GetSettings().Get("NServiceBus.AmazonSQS.S3BucketForLargeMessages"));
        }
        
        [Test]
        public void throws_if_s3_bucket_is_specified_without_key_prefix()
        {
            var sut = SUT();

            Assert.Throws<ArgumentNullException>(() => sut.S3BucketForLargeMessages("myTestBucket", string.Empty));
        }
        
        [Test]
        public void parsing_max_ttl_days_works()
        {
            var sut = SUT();

            var result = sut.MaxTTLDays(1);
            
            Assert.AreEqual(1, result.GetSettings().Get("NServiceBus.AmazonSQS.MaxTTLDays"));
        }

        [Test]
        public void invalid_max_ttl_days_throws()
        {
            var sut = SUT();

            Assert.Throws<ArgumentException>(() => sut.MaxTTLDays(100));
        }
        
        [Test]
        public void parsing_queue_name_prefix_works()
        {
            var sut = SUT();

            var result = sut.QueueNamePrefix("DEV");

            Assert.AreEqual("DEV", result.GetSettings().Get("NServiceBus.AmazonSQS.QueueNamePrefix"));
        }

        [Test]
        public void parsing_instance_profile_credential_source_works()
        {
            var sut = SUT();

            var result = sut.CredentialSource(SqsCredentialSource.InstanceProfile);

            Assert.AreEqual(SqsCredentialSource.InstanceProfile, result.GetSettings().Get("NServiceBus.AmazonSQS.CredentialSource"));
        }

        [Test]
        public void parsing_environment_variables_credential_source_works()
        {
            var sut = SUT();

            var result = sut.CredentialSource(SqsCredentialSource.EnvironmentVariables);

            Assert.AreEqual(SqsCredentialSource.EnvironmentVariables, result.GetSettings().Get("NServiceBus.AmazonSQS.CredentialSource"));
        }
        
        [Test]
        public void parsing_proxy_host_and_port_works()
        {
            var sut = SUT();

            var result = sut.Proxy("localhost", 8080);

            Assert.AreEqual("localhost", result.GetSettings().Get("NServiceBus.AmazonSQS.ProxyHost"));
            Assert.AreEqual(8080, result.GetSettings().Get("NServiceBus.AmazonSQS.ProxyPort"));
        }
    }
}
