namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
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
            cancellationTokenSource = new CancellationTokenSource();

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

        async Task SetupInitializedPump()
        {
            transport.UnrestrictedDurationDelayedDelivery(TimeSpan.FromMinutes(15));

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "900"},
                    {SQSConstants.ATTRIBUTE_MESSAGE_RETENTION_PERIOD, TimeSpan.FromDays(4).TotalSeconds.ToString(CultureInfo.InvariantCulture)},
                }
            };
            await pump.Initialize("queue", "queueUrl");
        }

        [Test]
        public async Task Start_loops_until_canceled()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                token.ThrowIfCancellationRequested();
                return new ReceiveMessageResponse { Messages = new List<Message>() };
            };

            pump.Start(cancellationTokenSource.Token);

            SpinWait.SpinUntil(() => mockSqsClient.ReceiveMessagesRequestsSent.Count > 0);

            cancellationTokenSource.Cancel();

            await pump.Stop();

            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r =>
            {
                return
                    r.MaxNumberOfMessages == 10 &&
                    r.QueueUrl == "queue-delay.fifo" &&
                    r.WaitTimeSeconds == 20 &&
                    r.AttributeNames.SequenceEqual(new List<string> {"MessageDeduplicationId", "SentTimestamp", "ApproximateFirstReceiveTimestamp", "ApproximateReceiveCount"}) &&
                    r.MessageAttributeNames.SequenceEqual(new List<string> {"All"});
            }));
        }

        [Test]
        public async Task Consume_sends_due_messages_in_batches()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                var receivedMessages = new List<Message>();
                for (var i = 0; i < 10; i++)
                {
                    receivedMessages.Add(new Message
                    {
                        Attributes = new Dictionary<string, string>
                        {
                            { "SentTimestamp", "10" },
                            { "ApproximateFirstReceiveTimestamp", "15" },
                            { "ApproximateReceiveCount", "0" }
                        },
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                            { Headers.MessageId, new MessageAttributeValue { StringValue = Guid.NewGuid().ToString() }}
                        },
                        Body = new string('a', 50*1024)
                    });
                }

                return new ReceiveMessageResponse
                {
                    Messages = receivedMessages
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.RequestsSent);
            Assert.AreEqual(2, mockSqsClient.BatchRequestsSent.Count);
            Assert.AreEqual(5, mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.Count);
            Assert.AreEqual(5, mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.Count);
            Assert.IsTrue(mockSqsClient.BatchRequestsSent.Select(b => b.QueueUrl).All(x => x == "queueUrl"));
        }

        [Test]
        public async Task Consume_deletes_due_messages_in_batches()
        {
            await SetupInitializedPump();

            var firstMessageId = Guid.NewGuid().ToString();
            var secondMessageId = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages = new List<Message>
                    {
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = firstMessageId }}
                            },
                            Body = new string('a', 50*1024)
                        },
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = secondMessageId }}
                            },
                            Body = new string('a', 50*1024)
                        }
                    }
                };
            };

            mockSqsClient.BatchRequestResponse = req =>
            {
                var successful = new List<SendMessageBatchResultEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    successful.Add(new SendMessageBatchResultEntry { Id = requestEntry.Id });
                }
                return new SendMessageBatchResponse
                {

                    Successful = successful
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.DeleteMessageRequestsSent);
            Assert.AreEqual(1, mockSqsClient.DeleteMessageBatchRequestsSent.Count);
            Assert.AreEqual(2, mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.Count);
            Assert.IsTrue(mockSqsClient.DeleteMessageBatchRequestsSent.Select(b => b.QueueUrl).All(x => x == "queue-delay.fifo"));
        }

        DelayedMessagesPump pump;
        MockSqsClient mockSqsClient;
        TransportExtensions<SqsTransport> transport;
        CancellationTokenSource cancellationTokenSource;
    }
}