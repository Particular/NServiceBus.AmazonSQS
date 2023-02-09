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

            var strategy = new DefaultMessageExtractor();

            var canExtract = strategy.TryExtractIncomingMessage(message, message.MessageId, out _, out _, out var body);

            Assert.IsTrue(canExtract);
            Assert.That(body, Is.EqualTo(message.Body));
        }
    }
}
