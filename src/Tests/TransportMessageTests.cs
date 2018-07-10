namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using DeliveryConstraints;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using Performance.TimeToBeReceived;
    using Transport;

    [TestFixture]
    public class TransportMessageTests
    {
        static TimeSpan expectedTtbr = TimeSpan.MaxValue.Subtract(TimeSpan.FromHours(1));
        const string expectedReplyToAddress = "TestReplyToAddress";

        [Test]
        public void Defaults_TimeToBeReceived_to_TimeSpan_MaxTime_when_DiscardIfNotReceivedBefore_is_not_provided()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, new Dictionary<string, string>(), new byte[0]);
            var deliveryConstrants = new List<DeliveryConstraint>();

            var transportMessage = new TransportMessage(outgoingMessage, deliveryConstrants);

            Assert.AreEqual(TimeSpan.MaxValue.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived is not TimeSpan.MaxValue");
        }

        [Test]
        public void Populates_TimeToBeReceived_when_DiscardIfNotReceivedBefore_is_provided()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, new Dictionary<string, string>(), new byte[0]);
            var deliveryConstrants = new List<DeliveryConstraint>
            {
                new DiscardIfNotReceivedBefore(expectedTtbr)
            };

            var transportMessage = new TransportMessage(outgoingMessage, deliveryConstrants);

            Assert.AreEqual(expectedTtbr.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived is not the expected value");
        }

        [Test]
        public void Populates_TimeToBeReceived_when_TimeToBeReceived_Header_is_present()
        {
            var transportMessage = new TransportMessage();
            transportMessage.Headers = new Dictionary<string, string>
            {
                { TransportHeaders.TimeToBeReceived, expectedTtbr.ToString() }
            };

            Assert.AreEqual(expectedTtbr.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived does not match expected value.");
        }

        [Test]
        public void Adds_TimeToBeReceived_Header_when_property_value_is_provided()
        {
            var transportMessage = new TransportMessage
            {
                Headers = new Dictionary<string, string>(),
                TimeToBeReceived = expectedTtbr.ToString()
            };

            Assert.IsTrue(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header is missing");
            Assert.AreEqual(expectedTtbr.ToString(), transportMessage.Headers[TransportHeaders.TimeToBeReceived], "TimeToBeReceived header does not match expected value.");
        }

        [Test]
        public void Does_not_add_TimeToBeReceived_Header_when_property_is_set_to_null()
        {
            var transportMessage = new TransportMessage
            {
                Headers = new Dictionary<string, string>()
            };

            Assert.IsFalse(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header was populated");
        }

        [Test]
        public void Populates_ReplyToAddress_when_header_is_present()
        {            
            var outgoingMessage = new OutgoingMessage(string.Empty, new Dictionary<string, string>
            {
                { Headers.ReplyToAddress, expectedReplyToAddress }
            }, new byte[0]);

            var transportMessage = new TransportMessage(outgoingMessage, new List<DeliveryConstraint>());

            Assert.AreEqual(expectedReplyToAddress, transportMessage.ReplyToAddress.Queue, "ReplyToAddress is not the expected value");
        }

        [Test]
        public void ReplyToAddress_is_null_when_no_ReplyToAddress_header_is_present()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, new Dictionary<string, string>(), new byte[0]);

            var transportMessage = new TransportMessage(outgoingMessage, new List<DeliveryConstraint>());

            Assert.IsNull(transportMessage.ReplyToAddress, "ReplyToAddress is not null");
        }

        [Test]
        public void Adds_ReplyToAddress_Header_when_property_value_is_provided()
        {
            var transportMessage = new TransportMessage
            {
                Headers = new Dictionary<string, string>(),
                ReplyToAddress = new TransportMessage.Address {Queue = expectedReplyToAddress}
            };

            Assert.IsTrue(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header is missing");
            Assert.AreEqual(expectedReplyToAddress, transportMessage.Headers[Headers.ReplyToAddress], "ReplyToAddress header does not match expected value.");
        }

        [Test]
        public void Does_not_add_ReplyToAddress_Header_when_property_value_is_set_to_null()
        {
            var transportMessage = new TransportMessage
            {
                Headers = new Dictionary<string, string>(),
                ReplyToAddress = null
            };

            Assert.IsFalse(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header was created");
        }

        [Test]
        public void Can_be_built_from_serialized_v1_message()
        {
            var json = JsonConvert.SerializeObject(new
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.Empty.ToString()}
                },
                Body = "empty message",
                S3BodyKey = (string)null,
                TimeToBeReceived = expectedTtbr,
                ReplyToAddress = new
                {
                    Queue = expectedReplyToAddress,
                    Machine = Environment.MachineName
                }
            });

            var transportMessage = JsonConvert.DeserializeObject<TransportMessage>(json);

            Assert.IsTrue(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header is missing");
            Assert.AreEqual(expectedTtbr.ToString(), transportMessage.Headers[TransportHeaders.TimeToBeReceived], "TimeToBeReceived header does not match expected value.");
            Assert.IsTrue(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header is missing");
            Assert.AreEqual(expectedReplyToAddress, transportMessage.Headers[Headers.ReplyToAddress], "ReplyToAddress header does not match expected value.");
        }
    }
}