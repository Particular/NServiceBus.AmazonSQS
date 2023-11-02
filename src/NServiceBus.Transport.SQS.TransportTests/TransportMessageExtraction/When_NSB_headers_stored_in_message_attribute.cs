namespace TransportTests.TransportMessageExtraction
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Amazon.SQS.Model;
    using NServiceBus.Transport.SQS;
    using NUnit.Framework;

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
