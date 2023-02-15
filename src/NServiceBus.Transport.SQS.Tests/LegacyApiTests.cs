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

#pragma warning disable CS0618 // Type or member is obsolete
            transport.EnableV1CompatibilityMode();
#pragma warning restore CS0618 // Type or member is obsolete

            transport.MaxTimeToLive(TimeSpan.FromMinutes(42));
            transport.QueueNamePrefix("MyPrefix");
            transport.TopicNamePrefix("MyTopicPrefix");
            transport.TopicNameGenerator((type, name) => "42");
            transport.DoNotWrapOutgoingMessages();

#pragma warning disable CS0618 // Type or member is obsolete
            Assert.IsTrue(transport.Transport.EnableV1CompatibilityMode);
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.AreEqual(TimeSpan.FromMinutes(42), transport.Transport.MaxTimeToLive);
            Assert.AreEqual("MyPrefix", transport.Transport.QueueNamePrefix);
            Assert.AreEqual("MyTopicPrefix", transport.Transport.TopicNamePrefix);
            Assert.AreEqual("42", transport.Transport.TopicNameGenerator(null, null));
            Assert.IsTrue(transport.Transport.DoNotWrapOutgoingMessages);

        }
    }
}
