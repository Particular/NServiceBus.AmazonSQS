namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using Amazon.SQS.Model;
    using NUnit.Framework;
    using static TransportHeaders;


    [TestFixture]
    class MessageTranslationStrategyTests
    {
        [Test]
        public void Can_translate_native_message_with_explicit_type_name_set()
        {
            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [MessageTypeFullName] = new MessageAttributeValue
                    {
                        StringValue = "System.String"
                    }
                },
                Body = "message-body"
            };

            var strategy = new DefaultAmazonSqsMessageTranslationStrategy();

            var (messageId, transportMessage) = strategy.FromAmazonSqsMessage(message);

            Assert.That(messageId, Is.EqualTo(message.MessageId));
            Assert.That(transportMessage, Is.Not.Null);
            Assert.That(transportMessage.Body, Is.EqualTo(message.Body));
        }
    }
}
