namespace TransportTests.TransportMessageExtraction
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Amazon.SQS.Model;
    using NServiceBus;
    using NServiceBus.Transport.SQS;
    using NUnit.Framework;

    [TestFixture]
    class When_NSB_headers_stored_in_message_attribute
    {
        [Test]
        public void Can_extract_headers_from_message_attribute()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");
            var messageIdFromHeaders = Guid.NewGuid().ToString("N");
            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = messageIdFromHeaders,
                ["SomeKey"] = "SomeValue"
            };

            var message = new Message
            {
                MessageId = nativeMessageId,
                MessageAttributes =
                {
                    [TransportHeaders.Headers] = new MessageAttributeValue
                    {
                        StringValue = JsonSerializer.Serialize(headers)
                    }
                }
            };

            var (messageId, transportMessage) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.Not.Null, "Should set the messageId");
            Assert.That(transportMessage, Is.Not.Null, "Should generate a transport message");
            Assert.That(transportMessage.Headers, Is.Not.Null, "Headers should be set");
            // Existing headers should be preserved
            CheckHeader("SomeKey", "SomeValue");
            // MessageId from headers should be overwritten
            CheckHeader(Headers.MessageId, nativeMessageId);

            void CheckHeader(string key, string expectedValue)
            {
                Assert.That(transportMessage.Headers.TryGetValue(key, out var storedValue), Is.True, $"Expected header with key `{key}` is missing");
                Assert.That(storedValue, Is.EqualTo(expectedValue), $"Header with key `{key}` has incorrect value {storedValue}. Expected {expectedValue}");
            }
        }

        [Test]
        public void Can_extract_S3_Body_Key_from_native_message_attribute()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");
            var s3BodyKey = Guid.NewGuid().ToString("N");
            var nsbS3BodyKey = Guid.NewGuid().ToString("N");
            var headers = new Dictionary<string, string>
            {
                [TransportHeaders.S3BodyKey] = nsbS3BodyKey
            };

            var message = new Message
            {
                MessageId = nativeMessageId,
                MessageAttributes =
                {
                    [TransportHeaders.Headers] = new MessageAttributeValue
                    {
                        StringValue = JsonSerializer.Serialize(headers)
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

            Assert.That(bodyKey, Is.Not.EqualTo(nsbS3BodyKey), "S3 Body Key from message attributes should overwrite existing headers");
        }

        [Test]
        public void Throws_if_headers_message_attribute_is_not_json()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");

            var message = new Message
            {
                MessageId = nativeMessageId,
                MessageAttributes =
                {
                    [TransportHeaders.Headers] = new MessageAttributeValue
                    {
                        StringValue = "NOT JSON"
                    }
                }
            };

            Assert.Throws<JsonException>(
                () => InputQueuePump.ExtractTransportMessage(nativeMessageId, message),
                "Should throw an exception if the header message attribute is not json"
            );
        }
    }

    [TestFixture]
    class When_handling_pure_native_message()
    {
        [Test]
        public void Can_extract_serialized_transport_message()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");
            var originalTransportMessage = new TransportMessage
            {
                Body = "Body",
                Headers = new Dictionary<string, string>
                {
                    ["SomeKey"] = "SomeValue"
                },
                // TODO: Handle ReplyToAddress
                //ReplyToAddress = "ReplyToAddress",
                S3BodyKey = "S3BodyKey",
                // TODO: Handle Time to be Received
                //TimeToBeReceived = 
            };

            var message = new Message
            {
                MessageId = nativeMessageId,
                Body = JsonSerializer.Serialize(originalTransportMessage)
            };

            var (messageId, transportMessage) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.Not.Null, "Message Id should be set");
            Assert.That(transportMessage, Is.Not.Null, "Transport Message should be set");
            Assert.That(transportMessage.S3BodyKey, Is.EqualTo(originalTransportMessage.S3BodyKey), "S3 Body Key should be transferred");
            Assert.That(transportMessage.Body, Is.EqualTo(originalTransportMessage.Body), "Body should be transferred");
        }
    }
}
