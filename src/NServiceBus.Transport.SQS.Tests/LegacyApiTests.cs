namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using Amazon.SQS.Model;
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
            transport.MessageExtractor(new CustomerExtractor());
            transport.DoNotBase64EncodeOutgoingMessages();

            Assert.IsTrue(transport.Transport.EnableV1CompatibilityMode);
            Assert.AreEqual(TimeSpan.FromMinutes(42), transport.Transport.MaxTimeToLive);
            Assert.AreEqual("MyPrefix", transport.Transport.QueueNamePrefix);
            Assert.AreEqual("MyTopicPrefix", transport.Transport.TopicNamePrefix);
            Assert.AreEqual("42", transport.Transport.TopicNameGenerator(null, null));
            Assert.IsTrue(transport.Transport.MessageExtractor.GetType() == typeof(CustomerExtractor));
            Assert.IsTrue(transport.Transport.DoNotBase64EncodeOutgoingMessages);

        }

        class CustomerExtractor : IMessageExtractor
        {
            public bool TryExtractIncomingMessage(Message receivedMessage, string messageId, out Dictionary<string, string> headers, out string s3BodyKey, out string body) => throw new NotImplementedException();
        }

    }
}
