namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using NServiceBus.Transport.SQS.Extensions;
    using NServiceBus;
    using NUnit.Framework;
    using Performance.TimeToBeReceived;
    using Transport;
    using System.Buffers;

    [TestFixture]
    public class TransportMessageTests
    {
        [Test]
        public void Defaults_TimeToBeReceived_to_TimeSpan_MaxTime_when_DiscardIfNotReceivedBefore_is_not_provided()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, [], new byte[0]);

            var transportMessage = new TransportMessage(outgoingMessage, []);

            Assert.AreEqual(TimeSpan.MaxValue.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived is not TimeSpan.MaxValue");
        }

        [Test]
        public void Populates_TimeToBeReceived_when_DiscardIfNotReceivedBefore_is_provided()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, [], new byte[0]);
            var dispatchProperties = new DispatchProperties
            {
                DiscardIfNotReceivedBefore = new DiscardIfNotReceivedBefore(ExpectedTtbr)
            };

            var transportMessage = new TransportMessage(outgoingMessage, dispatchProperties);

            Assert.AreEqual(ExpectedTtbr.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived is not the expected value");
        }

        [Test]
        public void Populates_TimeToBeReceived_when_TimeToBeReceived_Header_is_present()
        {
            var transportMessage = new TransportMessage
            {
                Headers = new Dictionary<string, string>
                {
                    {TransportHeaders.TimeToBeReceived, ExpectedTtbr.ToString()}
                }
            };

            Assert.AreEqual(ExpectedTtbr.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived does not match expected value.");
        }

        [Test]
        public void Adds_TimeToBeReceived_Header_when_property_value_is_provided()
        {
            var transportMessage = new TransportMessage
            {
                Headers = [],
                TimeToBeReceived = ExpectedTtbr.ToString()
            };

            Assert.IsTrue(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header is missing");
            Assert.AreEqual(ExpectedTtbr.ToString(), transportMessage.Headers[TransportHeaders.TimeToBeReceived], "TimeToBeReceived header does not match expected value.");
        }

        [Test]
        public void Does_not_add_TimeToBeReceived_Header_when_property_is_set_to_null()
        {
            var transportMessage = new TransportMessage
            {
                Headers = []
            };

            Assert.IsFalse(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header was populated");
        }

        [Test]
        public void Populates_ReplyToAddress_when_header_is_present()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, new Dictionary<string, string>
            {
                {Headers.ReplyToAddress, ExpectedReplyToAddress}
            }, Array.Empty<byte>());

            var transportMessage = new TransportMessage(outgoingMessage, []);

            Assert.AreEqual(ExpectedReplyToAddress, transportMessage.ReplyToAddress.Value.Queue, "ReplyToAddress is not the expected value");
        }

        [Test]
        public void ReplyToAddress_is_null_when_no_ReplyToAddress_header_is_present()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, [], new byte[0]);

            var transportMessage = new TransportMessage(outgoingMessage, []);

            Assert.IsNull(transportMessage.ReplyToAddress, "ReplyToAddress is not null");
        }

        [Test]
        public void Adds_ReplyToAddress_Header_when_property_value_is_provided()
        {
            var transportMessage = new TransportMessage
            {
                Headers = [],
                ReplyToAddress = new TransportMessage.Address { Queue = ExpectedReplyToAddress }
            };

            Assert.IsTrue(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header is missing");
            Assert.AreEqual(ExpectedReplyToAddress, transportMessage.Headers[Headers.ReplyToAddress], "ReplyToAddress header does not match expected value.");
        }

        [Test]
        public void Does_not_add_ReplyToAddress_Header_when_property_value_is_set_to_null()
        {
            var transportMessage = new TransportMessage
            {
                Headers = [],
                ReplyToAddress = null
            };

            Assert.IsFalse(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header was created");
        }

        [Test]
        public void Can_be_built_from_serialized_v1_message()
        {
            var json = JsonSerializer.Serialize(new
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.Empty.ToString()}
                },
                Body = TransportMessage.EmptyMessage,
                S3BodyKey = (string)null,
                TimeToBeReceived = ExpectedTtbr.ToString(),
                ReplyToAddress = new TransportMessage.Address
                {
                    Queue = ExpectedReplyToAddress,
                    Machine = Environment.MachineName
                }
            });

            var transportMessage = JsonSerializer.Deserialize<TransportMessage>(json);

            Assert.IsTrue(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header is missing");
            Assert.AreEqual(ExpectedTtbr.ToString(), transportMessage.Headers[TransportHeaders.TimeToBeReceived], "TimeToBeReceived header does not match expected value.");
            Assert.IsTrue(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header is missing");
            Assert.AreEqual(ExpectedReplyToAddress, transportMessage.Headers[Headers.ReplyToAddress], "ReplyToAddress header does not match expected value.");
        }

        [Test]
        public void Can_be_built_from_serialized_message()
        {
            var json = JsonSerializer.Serialize(new
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.Empty.ToString()}
                },
                Body = TransportMessage.EmptyMessage,
                S3BodyKey = (string)null
            });

            var transportMessage = JsonSerializer.Deserialize<TransportMessage>(json);

            Assert.IsFalse(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), "TimeToBeReceived header was found");
            Assert.AreEqual(TimeSpan.MaxValue.ToString(), transportMessage.TimeToBeReceived, "TimeToBeReceived does not match expected value.");
            Assert.IsFalse(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), "ReplyToAddress header was found");
            Assert.IsNull(transportMessage.ReplyToAddress, "ReplyToAddress was not null.");
        }

        const string ExpectedReplyToAddress = "TestReplyToAddress";
        static readonly TimeSpan ExpectedTtbr = TimeSpan.MaxValue.Subtract(TimeSpan.FromHours(1));
    }
}