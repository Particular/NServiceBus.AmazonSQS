namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Extensions;
    using NUnit.Framework;

    [TestFixture]
    public class TransportMessageExtensionsTests
    {
        readonly ArrayPool<byte> arrayPool = ArrayPool<byte>.Shared;
        byte[] bodyBuffer;

        [TearDown]
        public void TearDown()
        {
            if (bodyBuffer != null)
            {
                arrayPool.Return(bodyBuffer);
            }
        }

        [Test]
        public async Task Empty_body_is_received_ok()
        {
            var messageId = Guid.NewGuid().ToString();
            var body = Array.Empty<byte>();
            var outgoingMessage = new OutgoingMessage(messageId, [], body);

            var transportMessage = new TransportMessage(outgoingMessage, []);

            (var receivedBodyArray, bodyBuffer) = await transportMessage.RetrieveBody(messageId, null, arrayPool);
            var receivedBody = Encoding.UTF8.GetString(receivedBodyArray.ToArray());

            CollectionAssert.AreEqual(receivedBodyArray.ToArray(), body);
            Assert.That(receivedBody, Is.Null.Or.Empty);
        }

        [Test]
        public async Task Null_body_is_received_ok()
        {
            var messageId = Guid.NewGuid().ToString();
            var outgoingMessage = new OutgoingMessage(messageId, [], null);

            var transportMessage = new TransportMessage(outgoingMessage, []);

            (var receivedBodyArray, bodyBuffer) = await transportMessage.RetrieveBody(messageId, null, arrayPool);
            var receivedBody = Encoding.UTF8.GetString(receivedBodyArray.ToArray());

            Assert.That(receivedBody, Is.Null.Or.Empty);
        }

        [Test]
        public async Task Empty_message_string_body_is_received_as_empty()
        {
            var transportMessage = new TransportMessage
            {
                Body = "empty message",
            };

            (var receivedBodyArray, bodyBuffer) = await transportMessage.RetrieveBody(Guid.NewGuid().ToString(), null, arrayPool);
            var receivedBody = Encoding.UTF8.GetString(receivedBodyArray.ToArray());

            Assert.That(receivedBody, Is.Null.Or.Empty);
        }
    }
}