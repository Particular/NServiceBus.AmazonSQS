namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.SQS.Model;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Extensibility;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using Routing;
    using Settings;
    using Transport;
    using Transports.SQS;

    [TestFixture]
    public class MessageDispatcherTests
    {
        static TimeSpan expectedTtbr = TimeSpan.MaxValue.Subtract(TimeSpan.FromHours(1));

        const string expectedReplyToAddress = "TestReplyToAddress";

        [Test]
        public async Task Sends_V1_compatible_payload_when_configured()
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.V1CompatibilityMode, true);

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {TransportHeaders.TimeToBeReceived, expectedTtbr.ToString()},
                        {Headers.ReplyToAddress, expectedReplyToAddress}
                    }, Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address"),
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsNotEmpty(mockSqsClient.RequestsSent, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            var bodyJson = JObject.Parse(request.MessageBody);

            Assert.IsTrue(bodyJson.ContainsKey("TimeToBeReceived"), "TimeToBeReceived not serialized");
            Assert.AreEqual(expectedTtbr.ToString(), bodyJson["TimeToBeReceived"].Value<string>(), "Expected TTBR mismatch");

            Assert.IsTrue(bodyJson.ContainsKey("ReplyToAddress"), "ReplyToAddress not serialized");
            Assert.IsTrue(bodyJson["ReplyToAddress"].HasValues, "ReplyToAddress is not an object");
            Assert.IsTrue(bodyJson["ReplyToAddress"].Value<JObject>().ContainsKey("Queue"), "ReplyToAddress does not have a Queue value");
            Assert.AreEqual(expectedReplyToAddress, bodyJson["ReplyToAddress"].Value<JObject>()["Queue"].Value<string>(), "Expected ReplyToAddress mismatch");
        }

        [Test]
        public async Task Does_not_send_extra_properties_in_payload_by_default()
        {
            var settings = new SettingsHolder();

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {TransportHeaders.TimeToBeReceived, expectedTtbr.ToString()},
                        {Headers.ReplyToAddress, expectedReplyToAddress}
                    }, Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address"),
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsNotEmpty(mockSqsClient.RequestsSent, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            IDictionary<string,JToken> bodyJson = JObject.Parse(request.MessageBody);

            Assert.IsFalse(bodyJson.ContainsKey("TimeToBeReceived"), "TimeToBeReceived serialized");
            Assert.IsFalse(bodyJson.ContainsKey("ReplyToAddress"), "ReplyToAddress serialized");
        }

        [Test]
        public async Task Includes_message_id_in_message_attributes()
        {
            var settings = new SettingsHolder();

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var expectedId = "1234";

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(expectedId, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address"),
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            var sentMessage = mockSqsClient.RequestsSent.First();
            Assert.AreEqual(expectedId, sentMessage.MessageAttributes[Headers.MessageId].StringValue);
        }

        [Test]
        public async Task Should_dispatch_isolated_operations()
        {
            var settings = new SettingsHolder();

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Isolated),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsEmpty(mockSqsClient.BatchRequestsSent);
            Assert.AreEqual(2, mockSqsClient.RequestsSent.Count);
            Assert.AreEqual("address1", mockSqsClient.RequestsSent.ElementAt(0).QueueUrl);
            Assert.AreEqual("address2", mockSqsClient.RequestsSent.ElementAt(1).QueueUrl);
        }

        [Test]
        public void Should_raise_queue_does_not_exists_for_delayed_delivery_for_isolated_dispatch()
        {
            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.UnrestrictedDurationDelayedDelivery();

            var mockSqsClient = new MockSqsClient();
            mockSqsClient.RequestResponse = req => throw new QueueDoesNotExistException("Queue does not exist");

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Isolated,
                    new List<DeliveryConstraint>
                    {
                        new DelayDeliveryWith(TimeSpan.FromMinutes(30))
                    }));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await dispatcher.Dispatch(transportOperations, transportTransaction, context));
            StringAssert.StartsWith("Destination 'address1' doesn't support delayed messages longer than", exception.Message);
        }

        [Test]
        public async Task Should_batch_non_isolated_operations()
        {
            var settings = new SettingsHolder();

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsEmpty(mockSqsClient.RequestsSent);
            Assert.AreEqual(2, mockSqsClient.BatchRequestsSent.Count);
            Assert.AreEqual("address1", mockSqsClient.BatchRequestsSent.ElementAt(0).QueueUrl);
            Assert.AreEqual("address2", mockSqsClient.BatchRequestsSent.ElementAt(1).QueueUrl);
        }

        [Test]
        public void Should_raise_queue_does_not_exists_for_delayed_delivery_for_non_isolated_dispatch()
        {
            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.UnrestrictedDurationDelayedDelivery();

            var mockSqsClient = new MockSqsClient();
            mockSqsClient.BatchRequestResponse = req => throw new QueueDoesNotExistException("Queue does not exist");

            var dispatcher = new MessageDispatcher(new TransportConfiguration(settings), null, mockSqsClient, new QueueUrlCache(mockSqsClient));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Default,
                    new List<DeliveryConstraint>
                    {
                        new DelayDeliveryWith(TimeSpan.FromMinutes(30))
                    }),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Default,
                    new List<DeliveryConstraint>
                    {
                        new DelayDeliveryWith(TimeSpan.FromMinutes(30))
                    }));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await dispatcher.Dispatch(transportOperations, transportTransaction, context));
            StringAssert.StartsWith("Unable to send batch '1/1'. Destination 'address1' doesn't support delayed messages longer than", exception.Message);
        }
    }
}
