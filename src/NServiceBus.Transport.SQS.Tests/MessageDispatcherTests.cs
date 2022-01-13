namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS.Model;
    using Configure;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Extensibility;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NUnit.Framework;
    using Routing;
    using Settings;
    using SQS;
    using Transport;

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
            var transportConfiguration = new TransportConfiguration(settings);

            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

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

            var transportConfiguration = new TransportConfiguration(settings);

            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

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

            IDictionary<string, JToken> bodyJson = JObject.Parse(request.MessageBody);

            Assert.IsFalse(bodyJson.ContainsKey("TimeToBeReceived"), "TimeToBeReceived serialized");
            Assert.IsFalse(bodyJson.ContainsKey("ReplyToAddress"), "ReplyToAddress serialized");
        }

        public static IEnumerable MessageIdConstraintsSource
        {
            get
            {
                yield return new TestCaseData(new List<DeliveryConstraint>
                {
                    new DelayDeliveryWith(TimeSpan.FromMinutes(30))
                });
                yield return new TestCaseData(new List<DeliveryConstraint>());
            }
        }

        [Theory]
        [TestCaseSource(nameof(MessageIdConstraintsSource))]
        public async Task Includes_message_id_in_message_attributes(List<DeliveryConstraint> constraints)
        {
            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.UnrestrictedDurationDelayedDelivery();

            var mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);

            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

            var expectedId = "1234";

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(expectedId, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address"),
                    DispatchConsistency.Isolated,
                    constraints));

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

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

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
        public async Task Should_dispatch_multicast_operations()
        {
            var settings = new SettingsHolder();

            var mockSnsClient = new MockSnsClient();

            var transportConfiguration = new TransportConfiguration(settings);

            var dispatcher = new MessageDispatcher(transportConfiguration, null, null, mockSnsClient, new QueueCache(null, transportConfiguration), new TopicCache(mockSnsClient, settings.SetupMessageMetadataRegistry(), transportConfiguration));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event))),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)))
                );

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(2, mockSnsClient.PublishedEvents.Count);

            var topics = mockSnsClient.PublishedEvents.Select(e => e.TopicArn).ToList();

            Assert.Contains("arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-MessageDispatcherTests-Event", topics);
            Assert.Contains("arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-MessageDispatcherTests-AnotherEvent", topics);
        }

        [Test]
        public async Task Should_deduplicate_if_compatibility_mode_is_enabled_and_subscription_found()
        {
            var settings = new SettingsHolder();

            var mockSnsClient = new MockSnsClient();
            var mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, mockSnsClient, new QueueCache(mockSqsClient, transportConfiguration), new TopicCache(mockSnsClient, settings.SetupMessageMetadataRegistry(), transportConfiguration));

            mockSnsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions = new List<Subscription>
                {
                    new Subscription { Endpoint = "arn:abc", SubscriptionArn = "arn:subscription" }
                }
            };

            var messageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, string>()
            {
                {Headers.EnclosedMessageTypes, typeof(Event).AssemblyQualifiedName},
                {Headers.MessageIntent, MessageIntentEnum.Publish.ToString() }
            };
            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event))),
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("abc"))
            );

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(1, mockSnsClient.PublishedEvents.Count);
            Assert.IsEmpty(mockSqsClient.RequestsSent);
            Assert.IsEmpty(mockSqsClient.BatchRequestsSent);
        }

        [Test]
        public async Task Should_not_deduplicate_if_compatibility_mode_is_enabled_and_no_subscription_found()
        {
            var settings = new SettingsHolder();

            var mockSnsClient = new MockSnsClient();
            var mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, mockSnsClient, new QueueCache(mockSqsClient, transportConfiguration), new TopicCache(mockSnsClient, settings.SetupMessageMetadataRegistry(), transportConfiguration));

            var messageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, string>()
            {
                {Headers.EnclosedMessageTypes, typeof(Event).AssemblyQualifiedName}
            };
            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event))),
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("abc"))
            );

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(1, mockSnsClient.PublishedEvents.Count);
            Assert.AreEqual(1, mockSqsClient.BatchRequestsSent.Count);
        }

        [Test]
        public async Task Should_not_dispatch_multicast_operation_if_topic_doesnt_exist()
        {
            var settings = new SettingsHolder();

            var mockSnsClient = new MockSnsClient
            {
                FindTopicAsyncResponse = topic => null
            };

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, null, mockSnsClient, new QueueCache(null, transportConfiguration), new TopicCache(mockSnsClient, settings.SetupMessageMetadataRegistry(), transportConfiguration));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event)))
            );

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsEmpty(mockSnsClient.PublishedEvents);
            Assert.IsEmpty(mockSnsClient.CreateTopicRequests);
        }

        [Test]
        public async Task Should_not_dispatch_multicast_operation_if_event_type_is_object()
        {
            var settings = new SettingsHolder();

            var mockSnsClient = new MockSnsClient
            {
                //given that a subscriber never sets up a topic for object this has to return null
                FindTopicAsyncResponse = topic => null
            };

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, null, mockSnsClient, new QueueCache(null, transportConfiguration), new TopicCache(mockSnsClient, settings.SetupMessageMetadataRegistry(), transportConfiguration));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(object)))
            );

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.IsEmpty(mockSnsClient.PublishedEvents);
        }

        [Test]
        public async Task Should_upload_large_multicast_operations_request_to_s3()
        {
            string keyPrefix = "somePrefix";

            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.S3("someBucket", keyPrefix);

            var mockS3Client = new MockS3Client();
            var mockSnsClient = new MockSnsClient();

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, mockS3Client, null, mockSnsClient, new QueueCache(null, transportConfiguration), new TopicCache(mockSnsClient, settings.SetupMessageMetadataRegistry(), transportConfiguration));

            var longBodyMessageId = Guid.NewGuid().ToString();
            /* Crazy long message id will cause the message to go over limits because attributes count as well */
            var crazyLongMessageId = new string('x', 256 * 1024);
            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(longBodyMessageId, new Dictionary<string, string>(), Encoding.Default.GetBytes(new string('x', 256 * 1024))),
                    new MulticastAddressTag(typeof(Event)),
                    DispatchConsistency.Isolated),
                new TransportOperation(
                    new OutgoingMessage(crazyLongMessageId, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)),
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(2, mockSnsClient.PublishedEvents.Count);
            Assert.AreEqual(2, mockS3Client.PutObjectRequestsSent.Count);

            var longBodyMessageUpload = mockS3Client.PutObjectRequestsSent.Single(por => por.Key == $"{keyPrefix}/{longBodyMessageId}");
            var crazyLongMessageUpload = mockS3Client.PutObjectRequestsSent.Single(por => por.Key == $"{keyPrefix}/{crazyLongMessageId}");

            Assert.AreEqual("someBucket", longBodyMessageUpload.BucketName);
            Assert.AreEqual("someBucket", crazyLongMessageUpload.BucketName);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{longBodyMessageUpload.Key}", mockSnsClient.PublishedEvents.Single(pr => pr.MessageAttributes[Headers.MessageId].StringValue == longBodyMessageId).Message);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{crazyLongMessageUpload.Key}", mockSnsClient.PublishedEvents.Single(pr => pr.MessageAttributes[Headers.MessageId].StringValue == crazyLongMessageId).Message);
        }

        [Test]
        public void Should_raise_queue_does_not_exists_for_delayed_delivery_for_isolated_dispatch()
        {
            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.UnrestrictedDurationDelayedDelivery();

            var mockSqsClient = new MockSqsClient
            {
                RequestResponse = req => throw new QueueDoesNotExistException("Queue does not exist")
            };

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

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

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

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

            var mockSqsClient = new MockSqsClient
            {
                BatchRequestResponse = req => throw new QueueDoesNotExistException("Queue does not exist")
            };

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

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

        [Test]
        public async Task Should_dispatch_non_batched_all_messages_that_failed_in_batch()
        {
            var settings = new SettingsHolder();

            var mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, null, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

            var firstMessageIdThatWillFail = Guid.NewGuid().ToString();
            var secondMessageIdThatWillFail = Guid.NewGuid().ToString();

            mockSqsClient.BatchRequestResponse = req =>
            {
                var firstMessageMatch = req.Entries.SingleOrDefault(x => x.MessageAttributes[Headers.MessageId].StringValue == firstMessageIdThatWillFail);
                if (firstMessageMatch != null)
                {
                    return new SendMessageBatchResponse
                    {
                        Failed = new List<Amazon.SQS.Model.BatchResultErrorEntry>
                        {
                            new Amazon.SQS.Model.BatchResultErrorEntry
                            {
                                Id = firstMessageMatch.Id,
                                Message = "You know why"
                            }
                        }
                    };
                }

                var secondMessageMatch = req.Entries.SingleOrDefault(x => x.MessageAttributes[Headers.MessageId].StringValue == secondMessageIdThatWillFail);
                if (secondMessageMatch != null)
                {
                    return new SendMessageBatchResponse
                    {
                        Failed = new List<Amazon.SQS.Model.BatchResultErrorEntry>
                        {
                            new Amazon.SQS.Model.BatchResultErrorEntry
                            {
                                Id = secondMessageMatch.Id,
                                Message = "You know why"
                            }
                        }
                    };
                }

                return new SendMessageBatchResponse();
            };

            var firstMessageThatWillBeSuccessful = Guid.NewGuid().ToString();
            var secondMessageThatWillBeSuccessful = Guid.NewGuid().ToString();

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(firstMessageIdThatWillFail, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(firstMessageThatWillBeSuccessful, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(secondMessageThatWillBeSuccessful, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(secondMessageIdThatWillFail, new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(2, mockSqsClient.RequestsSent.Count);
            Assert.AreEqual(firstMessageIdThatWillFail, mockSqsClient.RequestsSent.ElementAt(0).MessageAttributes[Headers.MessageId].StringValue);
            Assert.AreEqual(secondMessageIdThatWillFail, mockSqsClient.RequestsSent.ElementAt(1).MessageAttributes[Headers.MessageId].StringValue);

            Assert.AreEqual(2, mockSqsClient.BatchRequestsSent.Count);
            CollectionAssert.AreEquivalent(new[] { firstMessageIdThatWillFail, firstMessageThatWillBeSuccessful }, mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.Select(x => x.MessageAttributes[Headers.MessageId].StringValue));
            CollectionAssert.AreEquivalent(new[] { secondMessageIdThatWillFail, secondMessageThatWillBeSuccessful }, mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.Select(x => x.MessageAttributes[Headers.MessageId].StringValue));
        }

        [Test]
        public async Task Should_upload_large_non_isolated_operations_to_s3()
        {
            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.S3("someBucket", "somePrefix");

            var mockS3Client = new MockS3Client();
            var mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, mockS3Client, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Default),
                new TransportOperation( /* Crazy long message id will cause the message to go over limits because attributes count as well */
                    new OutgoingMessage(new string('x', 256 * 1024), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(3, mockSqsClient.BatchRequestsSent.Count);
            Assert.AreEqual(3, mockS3Client.PutObjectRequestsSent.Count);

            var firstUpload = mockS3Client.PutObjectRequestsSent.ElementAt(0);
            var secondUpload = mockS3Client.PutObjectRequestsSent.ElementAt(1);
            var thirdUpload = mockS3Client.PutObjectRequestsSent.ElementAt(2);

            Assert.AreEqual("someBucket", firstUpload.BucketName);
            Assert.AreEqual("someBucket", secondUpload.BucketName);
            Assert.AreEqual("someBucket", thirdUpload.BucketName);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{firstUpload.Key}", mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.ElementAt(0).MessageBody);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{secondUpload.Key}", mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.ElementAt(0).MessageBody);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{thirdUpload.Key}", mockSqsClient.BatchRequestsSent.ElementAt(2).Entries.ElementAt(0).MessageBody);
        }

        [Test]
        public async Task Should_upload_large_isolated_operations_request_to_s3()
        {
            var settings = new SettingsHolder();
            var transportExtensions = new TransportExtensions<SqsTransport>(settings);
            transportExtensions.S3("someBucket", "somePrefix");

            var mockS3Client = new MockS3Client();
            var mockSqsClient = new MockSqsClient();

            var transportConfiguration = new TransportConfiguration(settings);
            var dispatcher = new MessageDispatcher(transportConfiguration, mockS3Client, mockSqsClient, null, new QueueCache(mockSqsClient, transportConfiguration), null);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address1"),
                    DispatchConsistency.Isolated),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), Encoding.Default.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Isolated),
                new TransportOperation( /* Crazy long message id will cause the message to go over limits because attributes count as well */
                    new OutgoingMessage(new string('x', 256 * 1024), new Dictionary<string, string>(), Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            await dispatcher.Dispatch(transportOperations, transportTransaction, context);

            Assert.AreEqual(3, mockSqsClient.RequestsSent.Count);
            Assert.AreEqual(3, mockS3Client.PutObjectRequestsSent.Count);

            var firstUpload = mockS3Client.PutObjectRequestsSent.ElementAt(0);
            var secondUpload = mockS3Client.PutObjectRequestsSent.ElementAt(1);
            var thirdUpload = mockS3Client.PutObjectRequestsSent.ElementAt(2);

            Assert.AreEqual("someBucket", firstUpload.BucketName);
            Assert.AreEqual("someBucket", secondUpload.BucketName);
            Assert.AreEqual("someBucket", thirdUpload.BucketName);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{firstUpload.Key}", mockSqsClient.RequestsSent.ElementAt(0).MessageBody);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{secondUpload.Key}", mockSqsClient.RequestsSent.ElementAt(1).MessageBody);
            StringAssert.Contains($@"""Body"":"""",""S3BodyKey"":""{thirdUpload.Key}", mockSqsClient.RequestsSent.ElementAt(2).MessageBody);
        }

        interface IEvent { }

        interface IMyEvent : IEvent { }
        class Event : IMyEvent { }
        class AnotherEvent : IMyEvent { }
    }
}
