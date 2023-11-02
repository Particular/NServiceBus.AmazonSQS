namespace TransportTests.TransportMessageExtraction
{
    using System;
    using Amazon.SQS.Model;
    using NServiceBus;
    using NServiceBus.Transport.SQS;
    using NUnit.Framework;

    [TestFixture]
    class TransportMessageExtractionMessageIdTests
    {
        string nativeMessageId = Guid.NewGuid().ToString("N");
        string nsbAttributeMessageId = Guid.NewGuid().ToString("N");

        [Test]
        public void Uses_message_attribute_when_set()
        {
            var message = new Message
            {
                MessageId = nativeMessageId,
                MessageAttributes =
                {
                    [Headers.MessageId] = new MessageAttributeValue
                    {
                        StringValue = nsbAttributeMessageId
                    }
                }
            };

            var (messageId, _) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.EqualTo(nsbAttributeMessageId), "Transport should use native message attribute with NSB Key for message Id when found");
        }

        [Test]
        public void Uses_native_message_id_when_attribute_not_set()
        {
            var message = new Message
            {
                MessageId = nativeMessageId,
            };

            var (messageId, _) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.EqualTo(nativeMessageId), "Transport should use native message Id for message Id when found");

        }
    }
}
