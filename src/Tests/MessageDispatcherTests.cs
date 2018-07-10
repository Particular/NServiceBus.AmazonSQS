namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
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
                    new UnicastAddressTag("address")));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsNotEmpty(mockSqsClient.RequestsSent, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            IDictionary<string, JToken> bodyJson = JObject.Parse(request.MessageBody);

            Assert.IsTrue(bodyJson.ContainsKey("TimeToBeReceived"), "TimeToBeReceived not serialized");
            Assert.AreEqual(expectedTtbr.ToString(), bodyJson["TimeToBeReceived"].Value<string>(), "Expected TTBR mismatch");

            Assert.IsTrue(bodyJson.ContainsKey("ReplyToAddress"), "ReplyToAddress not serialized");
            Assert.IsTrue(bodyJson["ReplyToAddress"].HasValues, "ReplyToAddress is not an object");
            Assert.IsTrue(bodyJson["ReplyToAddress"].Value<IDictionary<string, JToken>>().ContainsKey("Queue"), "ReplyToAddress does not have a Queue value");
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
                    new UnicastAddressTag("address")));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsNotEmpty(mockSqsClient.RequestsSent, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            IDictionary<string,JToken> bodyJson = JObject.Parse(request.MessageBody);

            Assert.IsFalse(bodyJson.ContainsKey("TimeToBeReceived"), "TimeToBeReceived serialized");
            Assert.IsFalse(bodyJson.ContainsKey("ReplyToAddress"), "ReplyToAddress serialized");
        }
    }
}
