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

            Assert.That(transportMessage.TimeToBeReceived, Is.EqualTo(TimeSpan.MaxValue.ToString()), "TimeToBeReceived is not TimeSpan.MaxValue");
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

            Assert.That(transportMessage.TimeToBeReceived, Is.EqualTo(ExpectedTtbr.ToString()), "TimeToBeReceived is not the expected value");
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

            Assert.That(transportMessage.TimeToBeReceived, Is.EqualTo(ExpectedTtbr.ToString()), "TimeToBeReceived does not match expected value.");
        }

        [Test]
        public void Adds_TimeToBeReceived_Header_when_property_value_is_provided()
        {
            var transportMessage = new TransportMessage
            {
                Headers = [],
                TimeToBeReceived = ExpectedTtbr.ToString()
            };

            Assert.Multiple(() =>
            {
                Assert.That(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), Is.True, "TimeToBeReceived header is missing");
                Assert.That(transportMessage.Headers[TransportHeaders.TimeToBeReceived], Is.EqualTo(ExpectedTtbr.ToString()), "TimeToBeReceived header does not match expected value.");
            });
        }

        [Test]
        public void Does_not_add_TimeToBeReceived_Header_when_property_is_set_to_null()
        {
            var transportMessage = new TransportMessage
            {
                Headers = []
            };

            Assert.That(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), Is.False, "TimeToBeReceived header was populated");
        }

        [Test]
        public void Populates_ReplyToAddress_when_header_is_present()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, new Dictionary<string, string>
            {
                {Headers.ReplyToAddress, ExpectedReplyToAddress}
            }, Array.Empty<byte>());

            var transportMessage = new TransportMessage(outgoingMessage, []);

            Assert.That(transportMessage.ReplyToAddress.Value.Queue, Is.EqualTo(ExpectedReplyToAddress), "ReplyToAddress is not the expected value");
        }

        [Test]
        public void ReplyToAddress_is_null_when_no_ReplyToAddress_header_is_present()
        {
            var outgoingMessage = new OutgoingMessage(string.Empty, [], new byte[0]);

            var transportMessage = new TransportMessage(outgoingMessage, []);

            Assert.That(transportMessage.ReplyToAddress, Is.Null, "ReplyToAddress is not null");
        }

        [Test]
        public void Adds_ReplyToAddress_Header_when_property_value_is_provided()
        {
            var transportMessage = new TransportMessage
            {
                Headers = [],
                ReplyToAddress = new TransportMessage.Address { Queue = ExpectedReplyToAddress }
            };

            Assert.Multiple(() =>
            {
                Assert.That(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), Is.True, "ReplyToAddress header is missing");
                Assert.That(transportMessage.Headers[Headers.ReplyToAddress], Is.EqualTo(ExpectedReplyToAddress), "ReplyToAddress header does not match expected value.");
            });
        }

        [Test]
        public void Does_not_add_ReplyToAddress_Header_when_property_value_is_set_to_null()
        {
            var transportMessage = new TransportMessage
            {
                Headers = [],
                ReplyToAddress = null
            };

            Assert.That(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), Is.False, "ReplyToAddress header was created");
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

            Assert.Multiple(() =>
            {
                Assert.That(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), Is.True, "TimeToBeReceived header is missing");
                Assert.That(transportMessage.Headers[TransportHeaders.TimeToBeReceived], Is.EqualTo(ExpectedTtbr.ToString()), "TimeToBeReceived header does not match expected value.");
                Assert.That(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), Is.True, "ReplyToAddress header is missing");
                Assert.That(transportMessage.Headers[Headers.ReplyToAddress], Is.EqualTo(ExpectedReplyToAddress), "ReplyToAddress header does not match expected value.");
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(transportMessage.Headers.ContainsKey(TransportHeaders.TimeToBeReceived), Is.False, "TimeToBeReceived header was found");
                Assert.That(transportMessage.TimeToBeReceived, Is.EqualTo(TimeSpan.MaxValue.ToString()), "TimeToBeReceived does not match expected value.");
                Assert.That(transportMessage.Headers.ContainsKey(Headers.ReplyToAddress), Is.False, "ReplyToAddress header was found");
                Assert.That(transportMessage.ReplyToAddress, Is.Null, "ReplyToAddress was not null.");
            });
        }

        const string ExpectedReplyToAddress = "TestReplyToAddress";
        static readonly TimeSpan ExpectedTtbr = TimeSpan.MaxValue.Subtract(TimeSpan.FromHours(1));
    }
}