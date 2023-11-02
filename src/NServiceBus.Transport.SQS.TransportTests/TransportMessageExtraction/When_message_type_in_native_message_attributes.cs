namespace TransportTests.TransportMessageExtraction
{
    using System;
    using Amazon.SQS.Model;
    using NServiceBus;
    using NServiceBus.Transport.SQS;
    using NUnit.Framework;

    [TestFixture]
    public class When_message_type_in_native_message_attributes
    {
        [Test]
        public void Can_extract_transport_message()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");
            var messageTypeFullName = "MessageTypeFullName";
            var body = "message body";

            var message = new Message
            {
                MessageId = nativeMessageId,
                MessageAttributes =
                {
                    [TransportHeaders.MessageTypeFullName] = new MessageAttributeValue
                    {
                        StringValue = messageTypeFullName,
                    }
                },
                Body = body
            };

            var (messageId, transportMessage) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.Not.Null, "Message Id should be set");
            Assert.That(transportMessage, Is.Not.Null, "Transport Message should be set");

            Assert.That(transportMessage.Body, Is.EqualTo(body), "Message body should be set to the raw value");
            Assert.That(transportMessage.Headers, Is.Not.Null, "Message headers should be set");

            // Should lift to enclosed message types
            CheckHeader(Headers.EnclosedMessageTypes, messageTypeFullName);
            // Should lift to equivalent key
            CheckHeader(TransportHeaders.MessageTypeFullName, messageTypeFullName);
            // Should lift message id
            CheckHeader(Headers.MessageId, nativeMessageId);

            void CheckHeader(string key, string expectedValue)
            {
                Assert.That(transportMessage.Headers.TryGetValue(key, out var actualValue), $"Header with key `{key}` should be set");
                Assert.That(actualValue, Is.EqualTo(expectedValue), $"Header with key {key} has incorrect value. Expected {expectedValue}. Actual {actualValue}");

            }
        }

        [Test]
        public void Can_extract_S3_Body_Key_from_native_message_attribute()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");
            var s3BodyKey = Guid.NewGuid().ToString("N");
            var messageTypeFullName = "MessageTypeFullName";

            var message = new Message
            {
                MessageId = nativeMessageId,
                MessageAttributes =
                {
                    [TransportHeaders.MessageTypeFullName] = new MessageAttributeValue
                    {
                        StringValue = messageTypeFullName,
                    },
                    [TransportHeaders.S3BodyKey] = new MessageAttributeValue
                    {
                        StringValue = s3BodyKey
                    }

                }
            };

            var (messageId, transportMessage) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.Not.Null, "Should set the messageId");
            Assert.That(transportMessage, Is.Not.Null, "Should generate a transport message");
            Assert.That(transportMessage.Headers, Is.Not.Null, "Headers should be set");

            Assert.That(transportMessage.S3BodyKey, Is.EqualTo(s3BodyKey), "S3 Body Key should be set on transport message");
            Assert.That(transportMessage.Headers.TryGetValue(TransportHeaders.S3BodyKey, out var bodyKey), Is.True, "S3 Body Key should be lifted into headers");
            Assert.That(bodyKey, Is.EqualTo(s3BodyKey), "S3 Body Key value lifted into headers should match native message attribute");
        }
    }
}
