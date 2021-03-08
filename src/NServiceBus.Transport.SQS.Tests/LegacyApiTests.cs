#pragma warning disable 618
namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class LegacyApiTests
    {

        [Test]
        public void Legacy_api_shim_sets_corresponding_new_api_properties()
        {
            var config = new EndpointConfiguration("MyEndpoint");

            var transport = config.UseTransport<SqsTransport>();

            transport.EnableV1CompatibilityMode();
            transport.MaxTimeToLive(TimeSpan.FromMinutes(42));
            transport.QueueNamePrefix("MyPrefix");
            transport.TopicNamePrefix("MyTopicPrefix");
            transport.TopicNameGenerator((type, name) => "42");

            Assert.IsTrue(transport.SqsTransport.EnableV1CompatibilityMode);
            Assert.AreEqual(TimeSpan.FromMinutes(42), transport.SqsTransport.MaxTimeToLive);
            Assert.AreEqual("MyPrefix", transport.SqsTransport.QueueNamePrefix);
            Assert.AreEqual("MyTopicPrefix", transport.SqsTransport.TopicNamePrefix);
            Assert.AreEqual("42", transport.SqsTransport.TopicNameGenerator(null, null));

        }
    }
}
#pragma warning restore 618
