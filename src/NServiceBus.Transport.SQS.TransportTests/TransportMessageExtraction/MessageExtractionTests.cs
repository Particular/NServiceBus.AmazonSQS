namespace TransportTests.TransportMessageExtraction
{
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

            #region NSB headers in message attribute tests
            yield return TestCase(
                "Transport headers in message attribute",
                native => native
                    .WithMessageAttributeHeader(TransportHeaders.S3BodyKey, "Will be overwritten", condition: pushBodyToS3)
                    .WithMessageAttributeHeader("SomeKey", "SomeValue")
                    .WithMessageAttributeHeader(Headers.MessageId, "Will be overwritten", condition: passMessageIdInNsbHeaders)
                    .WithMessageAttribute(TransportHeaders.S3BodyKey, "S3 Body Key", condition: pushBodyToS3)
                    .WithBody("Body Contents", condition: passBodyInMessage),
                transport => transport
                    // HINT: Message Id from headers is going to get overwritten no matter what
                    .WithHeader(Headers.MessageId, nativeMessageId)
                    .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughMessageAttribute, condition: passMessageIdInMessageAttribute)
                    .WithHeader("SomeKey", "SomeValue")
                    .WithBody("Body Contents", condition: passBodyInMessage)
                    .WithHeader(TransportHeaders.S3BodyKey, "S3 Body Key", condition: pushBodyToS3)
                    .WithS3BodyKey("S3 Body Key", condition: pushBodyToS3)
            );

            yield return TestCase(
                "Corrupted headers in message attribute",
                native => native.WithMessageAttribute(TransportHeaders.Headers, "NOT JSON DICTIONARY"),
                considerPoison: true);

            #endregion

            #region Message type in message attribute tests
            yield return TestCase(
                "Message type in message attributes",
                native => native
                    .WithMessageAttribute(TransportHeaders.MessageTypeFullName, "Message type full name")
                    .WithMessageAttribute(TransportHeaders.S3BodyKey, "S3 body key", condition: pushBodyToS3)
                    .WithBody("Body Contents", condition: passBodyInMessage),
                transport => transport
                    .WithHeader(TransportHeaders.MessageTypeFullName, "Message type full name")
                    .WithHeader(Headers.EnclosedMessageTypes, "Message type full name")
                    .WithBody("Body Contents", condition: passBodyInMessage)
                    .WithHeader(TransportHeaders.S3BodyKey, "S3 body key", condition: pushBodyToS3)
                    .WithS3BodyKey("S3 body key", condition: pushBodyToS3),
                // HINT: There is no way to pass this via headers. It should fall back to native message id
                expectedMessageId: passMessageIdInNsbHeaders && !passMessageIdInMessageAttribute ? nativeMessageId : null
            );
            #endregion

            #region Serialized transport message tests
            var senderTransportMessage = new TransportMessageBuilder()
                .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders, condition: passMessageIdInNsbHeaders)
                .WithBody("Body Contents", condition: passBodyInMessage)
                .WithS3BodyKey("S3 Body Key", condition: pushBodyToS3)
                .Build();

            yield return TestCase(
                "Serialized transport message",
                native => native
                    .WithBody(JsonSerializer.Serialize(senderTransportMessage)),
                transport => transport
                    // HINT: This is needed here because the serializer reads it and it gets a default (MAX). When it is deserialized it gets included
                    .WithHeader(TransportHeaders.TimeToBeReceived, TimeSpan.MaxValue.ToString())
                    // HINT: If the message id is passed via headers then it is used, otherwise no message id is set
                    .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders, condition: passMessageIdInNsbHeaders)
                    .WithBody("Body Contents", condition: passBodyInMessage)
                    .WithS3BodyKey("S3 Body Key", condition: pushBodyToS3)
            );

            #region Corrupted transport message tests
            // HINT: These should all throw
            yield return TestCase(
                "Corrupted serialized transport message no headers",
                native => native.WithBody(@"{
                                                ""Body"": ""Body Contents""
                                            }"),
                considerPoison: true
            );

            yield return TestCase(
                "Fully corrupted serialized transport message",
                native => native
                    .WithBody(@"{
                                    ""NonExistingProperty"": ""Does not matter""
                                }"),
                considerPoison: true
            );

            yield return TestCase(
                "Corrupted headers on serialized transport message",
                native => native
                    .WithBody(@"{
                                    ""Headers"": ""NOT A JSON DICTIONARY""
                                }"),
                considerPoison: true
            );

            // TODO: Add more test cases with malformed transport message objects
            #endregion

            #endregion

            TestCaseData TestCase(
                string name,
                Action<NativeMessageBuilder> native = null,
                Action<TransportMessageBuilder> transport = null,
                bool considerPoison = false,
                string expectedMessageId = null)
            {
                var messageBuilder = new NativeMessageBuilder(nativeMessageId)
                    .WithMessageAttribute(Headers.MessageId, nsbMessageIdPassedThroughMessageAttribute, passMessageIdInMessageAttribute);

                native?.Invoke(messageBuilder);

                var transportMessageBuilder = new TransportMessageBuilder()
                    // HINT: Last in wins
                    .WithHeader(Headers.MessageId, nativeMessageId)
                    .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughHeaders, condition: passMessageIdInNsbHeaders)
                    .WithHeader(Headers.MessageId, nsbMessageIdPassedThroughMessageAttribute, condition: passMessageIdInMessageAttribute)
                    .WithHeader(Headers.MessageId, expectedMessageId, condition: expectedMessageId != null);

                transport?.Invoke(transportMessageBuilder);

                var testCaseName = name;
                testCaseName += passMessageIdInMessageAttribute ? " - Message Id in message attribute" : " - Message Id NOT in message attribute";
                testCaseName += passMessageIdInNsbHeaders ? " - NSB message Id provided" : " - NSB message Id NOT provided";
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
            Assert.That(transportMessage.Headers, Is.Not.Null, "Headers should be set");

            Assert.That(transportMessage.Body, Is.EqualTo(expectedTransportMessage.Body), "Body is not set correctly");
            Assert.That(transportMessage.S3BodyKey, Is.EqualTo(expectedTransportMessage.S3BodyKey), "S3 Body Key is not set correctly");
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

            public NativeMessageBuilder WithMessageAttributeHeader(string key, string value, bool condition = true)
            {
                if (condition)
                {
                    headers[key] = value;
                }
                return this;
            }

            public NativeMessageBuilder WithMessageAttribute(string key, string value, bool condition = true)
            {
                if (condition)
                {
                    message.MessageAttributes[key] = new MessageAttributeValue { StringValue = value };
                }
                return this;
            }

            public NativeMessageBuilder WithBody(string body, bool condition = true)
            {
                if (condition)
                {
                    message.Body = body;
                }
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

            public TransportMessageBuilder WithHeader(string key, string value, bool condition = true)
            {
                if (condition)
                {
                    message.Headers[key] = value;
                }
                return this;
            }

            public TransportMessageBuilder WithBody(string body, bool condition = true)
            {
                if (condition)
                {
                    message.Body = body;
                }
                return this;
            }

            public TransportMessageBuilder WithS3BodyKey(string key, bool condition = true)
            {
                if (condition)
                {
                    message.S3BodyKey = key;
                }
                return this;
            }

            public TransportMessage Build() => message;
        }
    }
}
