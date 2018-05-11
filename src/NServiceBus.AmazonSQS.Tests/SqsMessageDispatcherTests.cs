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
    public partial class SqsMessageDispatcherTests
    {
        static TimeSpan expectedTtbr = TimeSpan.MaxValue.Subtract(TimeSpan.FromHours(1));
        const string expectedReplyToAddress = "TestReplyToAddress";

        [Test]
        public async Task Sends_V1_compatible_payload_when_configured()
        {
            var settings = new SettingsHolder();
            settings.Set(SqsTransportSettingsKeys.V1CompatibilityMode, true);

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new SqsMessageDispatcher
            {
                ConnectionConfiguration = new SqsConnectionConfiguration(settings),
                SqsClient = mockSqsClient,
                SqsQueueUrlCache = new SqsQueueUrlCache
                {
                    SqsClient = mockSqsClient
                }
            };

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {SqsTransportHeaders.TimeToBeReceived, expectedTtbr.ToString()},
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
            Assert.AreEqual(expectedReplyToAddress, bodyJson["ReplyToAddress"].Value<string>(), "Expected ReplyToAddress mismatch");
        }


        [Test]
        public async Task Does_not_send_extra_properties_in_payload_by_default()
        {
            var settings = new SettingsHolder();

            var mockSqsClient = new MockSqsClient();

            var dispatcher = new SqsMessageDispatcher
            {
                ConnectionConfiguration = new SqsConnectionConfiguration(settings),
                SqsClient = mockSqsClient,
                SqsQueueUrlCache = new SqsQueueUrlCache
                {
                    SqsClient = mockSqsClient
                }
            };

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {SqsTransportHeaders.TimeToBeReceived, expectedTtbr.ToString()},
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