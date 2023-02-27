namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Extensions;
    using NUnit.Framework;

    [TestFixture]
    public class TransportMessageExtensionsTests
    {
        [Test]
        public async Task Empty_body_is_received_ok()
        {
            var messageId = Guid.NewGuid().ToString();
            var body = new byte[0];
            var outgoingMessage = new OutgoingMessage(messageId, new Dictionary<string, string>(), body);

            var transportMessage = new TransportMessage(outgoingMessage, new List<DeliveryConstraint>());

            var receivedBodyArray = await transportMessage.RetrieveBody(null, null).ConfigureAwait(false);
            var receivedBody = Encoding.UTF8.GetString(receivedBodyArray);

            CollectionAssert.AreEqual(receivedBodyArray, body);
            Assert.That(receivedBody, Is.Null.Or.Empty);
        }

        [Test]
        public async Task Null_body_is_received_ok()
        {
            var messageId = Guid.NewGuid().ToString();
            var outgoingMessage = new OutgoingMessage(messageId, new Dictionary<string, string>(), null);

            var transportMessage = new TransportMessage(outgoingMessage, new List<DeliveryConstraint>());

            var receivedBodyArray = await transportMessage.RetrieveBody(null, null).ConfigureAwait(false);
            var receivedBody = Encoding.UTF8.GetString(receivedBodyArray);

            Assert.That(receivedBody, Is.Null.Or.Empty);
        }

        [Test]
        public async Task Empty_message_string_body_is_received_as_empty()
        {
            var transportMessage = new TransportMessage
            {
                Body = "empty message",
            };

            var receivedBodyArray = await transportMessage.RetrieveBody(null, null).ConfigureAwait(false);
            var receivedBody = Encoding.UTF8.GetString(receivedBodyArray);

            Assert.That(receivedBody, Is.Null.Or.Empty);
        }
    }
}