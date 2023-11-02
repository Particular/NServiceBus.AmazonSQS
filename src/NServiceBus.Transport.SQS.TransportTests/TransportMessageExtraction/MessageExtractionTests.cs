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
    class MessageExtractionTests
    {
        static IEnumerable<TestCaseData> TestCases
        {
            get
            {
                var nsbMessageId = Guid.NewGuid().ToString("N");

                yield return TestCase("Only native message Id");

                yield return TestCase("NSB Message Id in Message Attributes",
                    native => native.WithMessageAttribute(Headers.MessageId, nsbMessageId),
                    expectedMessageId: nsbMessageId);

                yield return TestCase(
                    "Passing headers via dedicated message attribute",
                    native => native.WithMessageAttribute(
                        TransportHeaders.Headers,
                              @"{
                                    ""SomeKey"": ""SomeValue""
                                }"),
                    transport => transport.WithHeader("SomeKey", "SomeValue")
                );

                yield return TestCase(
                    "Passing headers and S3 Body key via dedicated message attributes",
                    native => native
                        .WithMessageAttribute(TransportHeaders.Headers, "{}")
                        .WithMessageAttribute(TransportHeaders.S3BodyKey, "SomeS3BodyKey"),
                    transport => transport
                        .WithHeader(TransportHeaders.S3BodyKey, "SomeS3BodyKey")
                        .WithS3BodyKey("SomeS3BodyKey")
                );

                yield return TestCase(
                    "Passing message type full name as message attribute",
                    native => native
                        .WithMessageAttribute(TransportHeaders.MessageTypeFullName, "SomeTypeName")
                        .WithBody("Message Body"),
                    transport => transport
                        .WithHeader(Headers.EnclosedMessageTypes, "SomeTypeName")
                        .WithHeader(TransportHeaders.MessageTypeFullName, "SomeTypeName")
                        .WithBody("Message Body")
                );

                yield return TestCase(
                    "Passing message type full name and S3 body key as message attribute",
                    native => native
                        .WithMessageAttribute(TransportHeaders.MessageTypeFullName, "SomeTypeName")
                        .WithMessageAttribute(TransportHeaders.S3BodyKey, "S3 Body Key")
                        .WithBody("Message Body"),
                    transport => transport
                        .WithHeader(Headers.EnclosedMessageTypes, "SomeTypeName")
                        .WithHeader(TransportHeaders.S3BodyKey, "S3 Body Key")
                        .WithHeader(TransportHeaders.MessageTypeFullName, "SomeTypeName")
                        .WithS3BodyKey("S3 Body Key")
                        .WithBody("Message Body")
                );

                TestCaseData TestCase(string name, Action<NativeMessageBuilder> native = null, Action<TransportMessageBuilder> transport = null, string expectedMessageId = null)
                {
                    var nativeMessageId = Guid.NewGuid().ToString();
                    var messageBuilder = new NativeMessageBuilder(nativeMessageId);
                    native?.Invoke(messageBuilder);
                    var transportMessageBuilder = new TransportMessageBuilder();
                    transportMessageBuilder.WithHeader(Headers.MessageId, expectedMessageId ?? nativeMessageId);
                    transport?.Invoke(transportMessageBuilder);

                    return new TestCaseData(
                        nativeMessageId,
                        messageBuilder.Build(),
                        expectedMessageId ?? nativeMessageId,
                        transportMessageBuilder.Build()
                    ).SetName(name);
                }
            }
        }

        [Test, TestCaseSource(nameof(TestCases))]
        public void ExtractsMessageCorrectly(string nativeMessageId, Message message, string expectedMessageId, TransportMessage expectedTransportMessage)
        {
            var (messageId, transportMessage) = InputQueuePump.ExtractTransportMessage(nativeMessageId, message);

            Assert.That(messageId, Is.EqualTo(expectedMessageId), "Message Id set incorrectly");
            Assert.That(transportMessage, Is.Not.Null, "TransportMessage should be set");

            Assert.That(transportMessage.Body, Is.EqualTo(expectedTransportMessage.Body), "Body is not set correctly");
            Assert.That(transportMessage.S3BodyKey, Is.EqualTo(expectedTransportMessage.S3BodyKey), "S3 Body Key is not set correctly");
            // TODO: Handle ReplyToAddress and TimeToBeReceived
            //Assert.That(transportMessage.ReplyToAddress)
            //Assert.That(transportMessage.TimeToBeReceived)
            Assert.That(transportMessage.Headers, Is.EquivalentTo(expectedTransportMessage.Headers), "Headers are not set correctly");
        }

        [Test]
        public void Throws_if_headers_message_attribute_is_not_json()
        {
            var nativeMessageId = Guid.NewGuid().ToString("N");

            var message = new NativeMessageBuilder(nativeMessageId)
                .WithMessageAttribute(TransportHeaders.Headers, "NOT JSON")
                .Build();

            Assert.Throws<JsonException>(
                () => InputQueuePump.ExtractTransportMessage(nativeMessageId, message),
                "Should throw an exception if the header message attribute is not json"
            );
        }

        class NativeMessageBuilder
        {
            Message message;

            public NativeMessageBuilder(string nativeMessageId)
            {
                message = new Message { MessageId = nativeMessageId };
            }

            public NativeMessageBuilder WithMessageAttribute(string key, string value)
            {
                message.MessageAttributes[key] = new MessageAttributeValue { StringValue = value };
                return this;
            }

            public NativeMessageBuilder WithBody(string body)
            {
                message.Body = body;
                return this;
            }

            public Message Build() => message;
        }

        class TransportMessageBuilder
        {
            TransportMessage message = new TransportMessage
            {
                Headers = []
            };

            public TransportMessageBuilder WithHeader(string key, string value)
            {
                message.Headers[key] = value;
                return this;
            }

            public TransportMessageBuilder WithBody(string body)
            {
                message.Body = body;
                return this;
            }

            public TransportMessageBuilder WithS3BodyKey(string key)
            {
                message.S3BodyKey = key;
                return this;
            }

            public TransportMessage Build() => message;
        }
    }
}
