namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
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
            foreach (var passMessageIdInMessageAttribute in new[] { true, false })
            {
                foreach (var passMessageIdInNsbHeaders in new[] { true, false })
                {
                    foreach (var pushBodyToS3 in new[] { true, false })
                    {
                        foreach (var testCase in GenerateTestCases(passMessageIdInMessageAttribute, passMessageIdInNsbHeaders, pushBodyToS3))
                        {
                            yield return testCase;
                        }
                    }
                }
            }
        }
    }

    static IEnumerable<TestCaseData> GenerateTestCases(bool passMessageIdInMessageAttribute, bool passMessageIdInNsbHeaders, bool pushBodyToS3)
    {
        var nativeMessageId = "Native message Id";
        var nsbMessageIdPassedThroughMessageAttribute = "NSB Message Id passed via message attribute";
        var nsbMessageIdPassedThroughHeaders = "NSB Message Id passed via headers";
        bool passBodyInMessage = !pushBodyToS3;

        #region Raw message with no headers at all
        yield return TestCase(
            "Non-JSON message",
            native => native.WithBody("Body Contents"),
            transport => transport.WithBody("Body Contents"),
            expectedMessageId: nativeMessageId);

        yield return TestCase(
            "JSON without headers",
            native => native.WithBody("{}"),
            transport => transport.WithBody("{}"),
            expectedMessageId: nativeMessageId);

        yield return TestCase(
            "JSON without headers property with wrong type",
            native => native.WithBody(@"{ ""Headers"" : 6 }"),
            transport => transport.WithBody(@"{ ""Headers"" : 6 }"),
            expectedMessageId: nativeMessageId);

        yield return TestCase(
            "JSON with additional properties",
            native => native.WithBody(@"{ ""Headers"" : {}, ""Unexpected"" : null}"),
            transport => transport.WithBody(@"{ ""Headers"" : {}, ""Unexpected"" : null}"),
            expectedMessageId: nativeMessageId);

        yield return TestCase(
            "JSON with empty headers",
            native => native.WithBody(@"{ ""Headers"" : {} }"),
            transport => transport.WithBody(@"{ ""Headers"" : {} }"),
            expectedMessageId: nativeMessageId);

        yield return TestCase(
            "JSON with unrecognized headers",
            native => native.WithBody(@"{ ""Headers"" : { ""SomeHeader"" : ""some value""} }"),
            transport => transport.WithBody(@"{ ""Headers"" : { ""SomeHeader"" : ""some value""} }"),
            expectedMessageId: nativeMessageId);
        #endregion

        #region NSB headers in message attribute tests
        yield return TestCase(
            "Transport headers in message attribute",
            native => native
                .WithMessageAttributeHeader("SomeKey", "SomeValue")
                .If(passBodyInMessage, n => n
                    .WithBody("Body Contents"))
                .If(pushBodyToS3, n => n
                    .WithMessageAttributeHeader(TransportHeaders.S3BodyKey, "Will be overwritten")
                    .WithMessageAttribute(TransportHeaders.S3BodyKey, "S3 Body Key"))
                .If(passMessageIdInNsbHeaders, n => n
                    .WithMessageAttributeHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders)),
            transport => transport
                .WithHeader("SomeKey", "SomeValue")
                .If(passBodyInMessage, t => t
                    .WithBody("Body Contents"))
                .If(pushBodyToS3, t => t
                    .WithHeader(TransportHeaders.S3BodyKey, "S3 Body Key")
                    .WithS3BodyKey("S3 Body Key"))
        );

        yield return TestCase(
            "Corrupted headers in message attribute",
            native => native.WithMessageAttribute(TransportHeaders.Headers, "NOT JSON DICTIONARY"),
            considerPoison: true);

        yield return TestCase(
            "Preserve message ID from transport headers in message attribute",
            native => native
                .WithMessageAttribute(TransportHeaders.Headers, JsonSerializer.Serialize(new Dictionary<string, string> { { Headers.MessageId, nsbMessageIdPassedThroughHeaders } }))
                .WithBody("Body Contents"),
            transport => transport
                .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders)
                .WithBody("Body Contents")
        );
        #endregion

        #region Message type in message attribute tests
        yield return TestCase(
            "Message type in message attributes",
            native => native
                .WithMessageAttribute(TransportHeaders.MessageTypeFullName, "Message type full name")
                .If(passBodyInMessage, n => n
                    .WithBody("Body Contents"))
                .If(pushBodyToS3, n => n
                    .WithMessageAttribute(TransportHeaders.S3BodyKey, "S3 body key")),
            transport => transport
                .WithHeader(TransportHeaders.MessageTypeFullName, "Message type full name")
                .WithHeader(Headers.EnclosedMessageTypes, "Message type full name")
                .If(passBodyInMessage, t => t
                    .WithBody("Body Contents"))
                .If(pushBodyToS3, t => t
                    .WithHeader(TransportHeaders.S3BodyKey, "S3 body key")
                    .WithS3BodyKey("S3 body key")),
            // HINT: There is no way to pass this via headers. It should fall back to native or message attribute
            expectedMessageId: passMessageIdInMessageAttribute
                ? nsbMessageIdPassedThroughMessageAttribute
                : nativeMessageId
        );
        #endregion

        #region Serialized transport message tests
        var senderTransportMessage = new TransportMessageBuilder()
            .If(passMessageIdInNsbHeaders, t => t
                .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders))
            .WithHeader(Headers.EnclosedMessageTypes, "Enclosed message type")
            .If(passBodyInMessage, t => t
                .WithBody("Body Contents"))
            .If(pushBodyToS3, t => t
                .WithS3BodyKey("S3 Body Key"))
            .Build();

        yield return TestCase(
            "Serialized transport message",
            native => native
                .WithBody(JsonSerializer.Serialize(senderTransportMessage)),
            transport => transport
                // HINT: This is needed here because the serializer reads it and it gets a default (MAX). When it is deserialized it gets included
                .WithHeader(TransportHeaders.TimeToBeReceived, TimeSpan.MaxValue.ToString())
                .WithHeader(Headers.EnclosedMessageTypes, "Enclosed message type")
                .If(passBodyInMessage, t => t
                    .WithBody("Body Contents"))
                .If(pushBodyToS3, t => t
                    .WithS3BodyKey("S3 Body Key"))
        );

        #endregion

        TestCaseData TestCase(
            string name,
            Action<NativeMessageBuilder> native = null,
            Action<TransportMessageBuilder> transport = null,
            bool considerPoison = false,
            string expectedMessageId = null)
        {
            var messageBuilder = new NativeMessageBuilder(nativeMessageId)
                .If(passMessageIdInMessageAttribute, n => n
                    .WithMessageAttribute(Headers.MessageId, nsbMessageIdPassedThroughMessageAttribute));

            native?.Invoke(messageBuilder);

            var transportMessageBuilder = new TransportMessageBuilder()
                // HINT: Last in wins
                .WithHeader(Headers.MessageId, nativeMessageId)
                .If(passMessageIdInMessageAttribute, t => t
                    .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughMessageAttribute))
                .If(passMessageIdInNsbHeaders, t => t
                    .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders))
                .If(expectedMessageId != null, t => t
                    .WithHeader(Headers.MessageId, expectedMessageId));

            transport?.Invoke(transportMessageBuilder);

            var testCaseName = name;
            testCaseName += passMessageIdInMessageAttribute ? " - Message Id in message attribute" : " - Message Id NOT in message attribute";
            testCaseName += passMessageIdInNsbHeaders ? " - Message Id in NSB headers" : " - Message Id NOT in NSB headers";
            testCaseName += pushBodyToS3 ? " - body in S3" : " - body on message";

            return new TestCaseData(
                messageBuilder.Build(),
                transportMessageBuilder.Build(),
                considerPoison
            ).SetName(testCaseName);
        }
    }

    [TestCaseSource(nameof(TestCases))]
    public void ExtractsMessageCorrectly(Message message, TransportMessage expectedTransportMessage, bool considerPoison)
    {
        var messageId = InputQueuePump.ExtractMessageId(message);
        if (considerPoison)
        {
            Assert.Throws(
                Is.InstanceOf<Exception>(),
                () => InputQueuePump.ExtractTransportMessage(message, messageId),
                "This case is not supported. Message should be treated as poison."
            );
            return;
        }

        var transportMessage = InputQueuePump.ExtractTransportMessage(message, messageId);
        Assert.That(transportMessage, Is.Not.Null, "TransportMessage should be set");
        Assert.Multiple(() =>
        {
            Assert.That(transportMessage.Headers, Is.Not.Null, "Headers should be set");

            Assert.That(transportMessage.Body, Is.EqualTo(expectedTransportMessage.Body), "Body is not set correctly");
            Assert.That(transportMessage.S3BodyKey, Is.EqualTo(expectedTransportMessage.S3BodyKey), "S3 Body Key is not set correctly");
        });
        // TODO: Handle ReplyToAddress and TimeToBeReceived
        //Assert.That(transportMessage.ReplyToAddress)
        //Assert.That(transportMessage.TimeToBeReceived)

        Assert.That(transportMessage.Headers, Is.EquivalentTo(expectedTransportMessage.Headers), "Headers are not set correctly");
    }

    class NativeMessageBuilder
    {
        Message message;
        Dictionary<string, string> headers = [];

        public NativeMessageBuilder(string nativeMessageId)
        {
            message = new Message { MessageId = nativeMessageId };
        }

        public NativeMessageBuilder If(bool condition, Action<NativeMessageBuilder> action)
        {
            if (condition)
            {
                action(this);
            }
            return this;
        }

        public NativeMessageBuilder WithMessageAttributeHeader(string key, string value)
        {
            headers[key] = value;
            return this;
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

        public Message Build()
        {
            if (headers.Any())
            {
                var serialized = JsonSerializer.Serialize(headers);
                message.MessageAttributes.Add(TransportHeaders.Headers, new MessageAttributeValue
                {
                    StringValue = serialized
                });
                ;
            }
            return message;
        }
    }

    class TransportMessageBuilder
    {
        TransportMessage message = new TransportMessage
        {
            Headers = []
        };

        public TransportMessageBuilder If(bool condition, Action<TransportMessageBuilder> action)
        {
            if (condition)
            {
                action(this);
            }
            return this;
        }

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