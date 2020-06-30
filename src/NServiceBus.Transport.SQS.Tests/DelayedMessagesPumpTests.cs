namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Amazon.SQS.Model;
    using Amazon.SQS.Util;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class DelayedMessagesPumpTests
    {
        [SetUp]
        public void SetUp()
        {
            var settings = new SettingsHolder();
            transport = new TransportExtensions<SqsTransport>(settings);

            mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);

            pump = new DelayedMessagesPump(transportConfiguration, mockSqsClient, new QueueCache(mockSqsClient, transportConfiguration));
        }

        [Test]
        public void Initialize_delay_seconds_smaller_than_required_throws()
        {
            transport.UnrestrictedDurationDelayedDelivery(TimeSpan.FromMinutes(15));

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "1"}
                }
            };

            var exception = Assert.ThrowsAsync<Exception>(async () => { await pump.Initialize("queue", "queueUrl"); });
            Assert.AreEqual("Delayed delivery queue 'queue-delay.fifo' should not have Delivery Delay less than '00:15:00'.", exception.Message);
        }

        [Test]
        public void Initialize_retention_smaller_than_required_throws()
        {
            transport.UnrestrictedDurationDelayedDelivery(TimeSpan.FromMinutes(15));

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "900"},
                    {SQSConstants.ATTRIBUTE_MESSAGE_RETENTION_PERIOD, "10"}
                }
            };

            var exception = Assert.ThrowsAsync<Exception>(async () => { await pump.Initialize("queue", "queueUrl"); });
            Assert.AreEqual("Delayed delivery queue 'queue-delay.fifo' should not have Message Retention Period less than '4.00:00:00'.", exception.Message);
        }

        [Test]
        public void Initialize_redrive_policy_used_throws()
        {
            transport.UnrestrictedDurationDelayedDelivery(TimeSpan.FromMinutes(15));

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "900"},
                    {SQSConstants.ATTRIBUTE_MESSAGE_RETENTION_PERIOD, TimeSpan.FromDays(4).TotalSeconds.ToString(CultureInfo.InvariantCulture)},
                    {SQSConstants.ATTRIBUTE_REDRIVE_POLICY, "{}"}
                }
            };

            var exception = Assert.ThrowsAsync<Exception>(async () => { await pump.Initialize("queue", "queueUrl"); });
            Assert.AreEqual("Delayed delivery queue 'queue-delay.fifo' should not have Redrive Policy enabled.", exception.Message);
        }

        DelayedMessagesPump pump;
        MockSqsClient mockSqsClient;
        TransportExtensions<SqsTransport> transport;
    }
}