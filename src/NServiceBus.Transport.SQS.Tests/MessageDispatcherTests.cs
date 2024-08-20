namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS.Model;
    using Configure;
    using DelayedDelivery;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Approvals;
    using Routing;
    using Settings;
    using SQS;
    using Transport;

    [TestFixture]
    public class MessageDispatcherTests
    {
        static readonly TimeSpan ExpectedTtbr = TimeSpan.MaxValue.Subtract(TimeSpan.FromHours(1));

        const string ExpectedReplyToAddress = "TestReplyToAddress";

        [Test]
        public async Task Does_not_send_extra_properties_in_payload_by_default()
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {TransportHeaders.TimeToBeReceived, ExpectedTtbr.ToString()},
                        {Headers.ReplyToAddress, ExpectedReplyToAddress},
                        {Headers.MessageId, "093C17C6-D32E-44FE-9134-65C10C1287EB"}
                    }, Encoding.Default.GetBytes("{}")),
                    new UnicastAddressTag("address"),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSqsClient.RequestsSent, Is.Not.Empty, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            Approver.Verify(request.MessageBody);
        }

        public static IEnumerable MessageIdConstraintsSource
        {
            get
            {
                yield return new TestCaseData(new DispatchProperties
                {
                    DelayDeliveryWith = new DelayDeliveryWith(TimeSpan.FromMinutes(30))
                });

                yield return new TestCaseData(new DispatchProperties());
            }
        }

        [Theory]
        [TestCaseSource(nameof(MessageIdConstraintsSource))]
        public async Task Includes_message_id_in_message_attributes(DispatchProperties properties)
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var expectedId = "1234";

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(expectedId, [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address"),
                    properties,
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            var sentMessage = mockSqsClient.RequestsSent.First();
            Assert.That(sentMessage.MessageAttributes[Headers.MessageId].StringValue, Is.EqualTo(expectedId));
        }

        [Test]
        public async Task Should_dispatch_isolated_unicast_operations()
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    [],
                    DispatchConsistency.Isolated),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.BatchRequestsSent, Is.Empty);
                Assert.That(mockSqsClient.RequestsSent, Has.Count.EqualTo(2));
            });
            Assert.That(mockSqsClient.RequestsSent.Select(t => t.QueueUrl), Is.EquivalentTo(new[]
            {
                "address1",
                "address2"
            }));
        }

        [Test]
        public async Task Should_dispatch_isolated_multicast_operations()
        {
            var mockSnsClient = new MockSnsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), null, mockSnsClient, null,
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(),
                    new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""), null,
                15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event)),
                    [],
                    DispatchConsistency.Isolated),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.BatchRequestsPublished, Is.Empty);
                Assert.That(mockSnsClient.PublishedEvents, Has.Count.EqualTo(2));
            });
            Assert.That(mockSnsClient.PublishedEvents.Select(t => t.TopicArn), Is.EquivalentTo(new[]
            {
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-MessageDispatcherTests-Event",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-MessageDispatcherTests-AnotherEvent"
            }));
        }

        [Test]
        public async Task Should_deduplicate_if_compatibility_mode_is_enabled_and_subscription_found()
        {
            var mockSnsClient = new MockSnsClient();
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, mockSnsClient, new QueueCache(null,
                    dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""),
                null, 15 * 60);

            mockSnsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions =
                [
                    new Subscription { Endpoint = "arn:abc", SubscriptionArn = "arn:subscription" }
                ]
            };

            var messageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, string>()
            {
                {Headers.EnclosedMessageTypes, typeof(Event).AssemblyQualifiedName},
                {Headers.MessageIntent, MessageIntent.Publish.ToString() }
            };
            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event))),
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("abc"))
            );

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.BatchRequestsPublished, Has.Count.EqualTo(1));
                Assert.That(mockSqsClient.RequestsSent, Is.Empty);
                Assert.That(mockSqsClient.BatchRequestsSent, Is.Empty);
            });
        }

        [Test]
        public async Task Should_not_deduplicate_if_compatibility_mode_is_enabled_and_no_subscription_found()
        {
            var mockSnsClient = new MockSnsClient();
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, mockSnsClient, new QueueCache(mockSqsClient,
                    dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""),
                null, 15 * 60);

            var messageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, string>()
            {
                {Headers.EnclosedMessageTypes, typeof(Event).AssemblyQualifiedName}
            };
            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event))),
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("abc"))
            );

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.BatchRequestsPublished, Has.Count.EqualTo(1));
                Assert.That(mockSqsClient.BatchRequestsSent, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task Should_not_dispatch_multicast_operation_if_topic_doesnt_exist()
        {
            var mockSnsClient = new MockSnsClient
            {
                FindTopicAsyncResponse = topic => null
            };

            var dispatcher = new MessageDispatcher(new SettingsHolder(), null, mockSnsClient, new QueueCache(null,
                    dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""),
                null, 15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event)))
            );

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.PublishedEvents, Is.Empty);
                Assert.That(mockSnsClient.CreateTopicRequests, Is.Empty);
            });
        }

        [Test]
        public async Task Should_not_dispatch_multicast_operation_if_event_type_is_object()
        {
            var mockSnsClient = new MockSnsClient
            {
                //given that a subscriber never sets up a topic for object this has to return null
                FindTopicAsyncResponse = topic => null
            };

            var dispatcher = new MessageDispatcher(new SettingsHolder(), null, mockSnsClient, new QueueCache(null,
                    dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""),
                null, 15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(object)))
            );

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSnsClient.PublishedEvents, Is.Empty);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_upload_large_multicast_operations_request_to_s3(bool wrapMessage)
        {
            string keyPrefix = "somePrefix";

            var mockS3Client = new MockS3Client();
            var mockSnsClient = new MockSnsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), null, mockSnsClient, new QueueCache(null,
                    dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""),
                new S3Settings("someBucket", keyPrefix, mockS3Client), 15 * 60, wrapOutgoingMessages: wrapMessage);

            var longBodyMessageId = Guid.NewGuid().ToString();
            /* Crazy long message id will cause the message to go over limits because attributes count as well */
            var crazyLongMessageId = new string('x', 256 * 1024);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(longBodyMessageId, [], Encoding.UTF8.GetBytes(new string('x', 256 * 1024))),
                    new MulticastAddressTag(typeof(Event)),
                    [],
                    DispatchConsistency.Isolated),
                new TransportOperation( /* Crazy long message id will cause the message to go over limits because attributes count as well */
                    new OutgoingMessage(crazyLongMessageId, [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.PublishedEvents, Has.Count.EqualTo(2));
                Assert.That(mockS3Client.PutObjectRequestsSent, Has.Count.EqualTo(2));
            });

            var longBodyMessageUpload = mockS3Client.PutObjectRequestsSent.Single(por => por.Key == $"{keyPrefix}/{longBodyMessageId}");
            var crazyLongMessageUpload = mockS3Client.PutObjectRequestsSent.Single(por => por.Key == $"{keyPrefix}/{crazyLongMessageId}");

            Assert.Multiple(() =>
            {
                Assert.That(longBodyMessageUpload.BucketName, Is.EqualTo("someBucket"));
                Assert.That(crazyLongMessageUpload.BucketName, Is.EqualTo("someBucket"));
            });
            if (wrapMessage)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(mockSnsClient.PublishedEvents.Single(pr => pr.MessageAttributes[Headers.MessageId].StringValue == longBodyMessageId).Message, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{longBodyMessageUpload.Key}"));
                    Assert.That(mockSnsClient.PublishedEvents.Single(pr => pr.MessageAttributes[Headers.MessageId].StringValue == crazyLongMessageId).Message, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{crazyLongMessageUpload.Key}"));
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(mockSnsClient.PublishedEvents.Single(pr => pr.MessageAttributes[Headers.MessageId].StringValue == longBodyMessageId).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(longBodyMessageUpload.Key));
                    Assert.That(mockSnsClient.PublishedEvents.Single(pr => pr.MessageAttributes[Headers.MessageId].StringValue == crazyLongMessageId).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(crazyLongMessageUpload.Key));
                });
            }
        }

        [Test]
        public void Should_raise_queue_does_not_exists_for_delayed_delivery_for_isolated_dispatch()
        {
            var mockSqsClient = new MockSqsClient
            {
                RequestResponse = req => throw new QueueDoesNotExistException("Queue does not exist")
            };

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                    dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var properties = new DispatchProperties
            {
                DelayDeliveryWith = new DelayDeliveryWith(TimeSpan.FromMinutes(30))
            };

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    properties,
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await dispatcher.Dispatch(transportOperations, transportTransaction));
            Assert.That(exception.Message, Does.StartWith("Destination 'address1' doesn't support delayed messages longer than"));
        }

        [Test]
        public async Task Should_batch_non_isolated_unicast_operations()
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.RequestsSent, Is.Empty);
                Assert.That(mockSqsClient.BatchRequestsSent, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).QueueUrl, Is.EqualTo("address1"));
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).QueueUrl, Is.EqualTo("address2"));
            });
        }

        [Test]
        public async Task Should_batch_non_isolated_multicast_operations()
        {
            var mockSnsClient = new MockSnsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), null, mockSnsClient, new QueueCache(null,
                dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(),
                    new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""), null,
                15 * 60);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event)),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)),
                    [],
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.PublishedEvents, Is.Empty);
                Assert.That(mockSnsClient.BatchRequestsPublished, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.BatchRequestsPublished.ElementAt(0).TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-MessageDispatcherTests-Event"));
                Assert.That(mockSnsClient.BatchRequestsPublished.ElementAt(1).TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-MessageDispatcherTests-AnotherEvent"));
            });
        }

        [Test]
        public void Should_raise_queue_does_not_exists_for_delayed_delivery_for_non_isolated_dispatch()
        {
            var mockSqsClient = new MockSqsClient
            {
                BatchRequestResponse = req => throw new QueueDoesNotExistException("Queue does not exist")
            };

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var properties = new DispatchProperties
            {
                DelayDeliveryWith = new DelayDeliveryWith(TimeSpan.FromMinutes(30))
            };

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    properties,
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    properties,
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();

            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await dispatcher.Dispatch(transportOperations, transportTransaction));
            Assert.That(exception.Message, Does.StartWith("Unable to send batch '1/1'. Destination 'address1' doesn't support delayed messages longer than"));
        }

        [Test]
        public async Task Should_dispatch_non_batched_all_unicast_operations_that_failed_in_batch()
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var firstMessageIdThatWillFail = Guid.NewGuid().ToString();
            var secondMessageIdThatWillFail = Guid.NewGuid().ToString();

            mockSqsClient.BatchRequestResponse = req =>
            {
                var firstMessageMatch = req.Entries.SingleOrDefault(x => x.MessageAttributes[Headers.MessageId].StringValue == firstMessageIdThatWillFail);
                if (firstMessageMatch != null)
                {
                    return new SendMessageBatchResponse
                    {
                        Failed =
                        [
                            new()
                            {
                                Id = firstMessageMatch.Id,
                                Message = "You know why"
                            }
                        ]
                    };
                }

                var secondMessageMatch = req.Entries.SingleOrDefault(x => x.MessageAttributes[Headers.MessageId].StringValue == secondMessageIdThatWillFail);
                if (secondMessageMatch != null)
                {
                    return new SendMessageBatchResponse
                    {
                        Failed =
                        [
                            new()
                            {
                                Id = secondMessageMatch.Id,
                                Message = "You know why"
                            }
                        ]
                    };
                }

                return new SendMessageBatchResponse();
            };

            var firstMessageThatWillBeSuccessful = Guid.NewGuid().ToString();
            var secondMessageThatWillBeSuccessful = Guid.NewGuid().ToString();

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(firstMessageIdThatWillFail, [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(firstMessageThatWillBeSuccessful, [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address1"),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(secondMessageThatWillBeSuccessful, [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(secondMessageIdThatWillFail, [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSqsClient.RequestsSent, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.RequestsSent.ElementAt(0).MessageAttributes[Headers.MessageId].StringValue, Is.EqualTo(firstMessageIdThatWillFail));
                Assert.That(mockSqsClient.RequestsSent.ElementAt(1).MessageAttributes[Headers.MessageId].StringValue, Is.EqualTo(secondMessageIdThatWillFail));
            });
            Assert.That(mockSqsClient.BatchRequestsSent, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.Select(x => x.MessageAttributes[Headers.MessageId].StringValue), Is.EquivalentTo(new[] { firstMessageIdThatWillFail, firstMessageThatWillBeSuccessful }));
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.Select(x => x.MessageAttributes[Headers.MessageId].StringValue), Is.EquivalentTo(new[] { secondMessageIdThatWillFail, secondMessageThatWillBeSuccessful }));
            });
        }

        [Test]
        public async Task Should_dispatch_non_batched_all_multicast_operations_that_failed_in_batch()
        {
            var mockSnsClient = new MockSnsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), null, mockSnsClient, new QueueCache(null,
                dest => QueueCache.GetSqsQueueName(dest, "")), new TopicCache(mockSnsClient, new SettingsHolder(), new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => TopicNameHelper.GetSnsTopicName(type, ""), ""), null, 15 * 60);

            var firstMessageIdThatWillFail = Guid.NewGuid().ToString();
            var secondMessageIdThatWillFail = Guid.NewGuid().ToString();

            mockSnsClient.BatchRequestResponse = req =>
            {
                var firstMessageMatch = req.PublishBatchRequestEntries.SingleOrDefault(x => x.MessageAttributes[Headers.MessageId].StringValue == firstMessageIdThatWillFail);
                if (firstMessageMatch != null)
                {
                    return new PublishBatchResponse
                    {
                        Failed =
                        [
                            new()
                            {
                                Id = firstMessageMatch.Id,
                                Message = "You know why"
                            }
                        ]
                    };
                }

                var secondMessageMatch = req.PublishBatchRequestEntries.SingleOrDefault(x => x.MessageAttributes[Headers.MessageId].StringValue == secondMessageIdThatWillFail);
                if (secondMessageMatch != null)
                {
                    return new PublishBatchResponse
                    {
                        Failed =
                        [
                            new()
                            {
                                Id = secondMessageMatch.Id,
                                Message = "You know why"
                            }
                        ]
                    };
                }

                return new PublishBatchResponse();
            };

            var firstMessageThatWillBeSuccessful = Guid.NewGuid().ToString();
            var secondMessageThatWillBeSuccessful = Guid.NewGuid().ToString();

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(firstMessageIdThatWillFail, [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event)),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(firstMessageThatWillBeSuccessful, [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(Event)),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(secondMessageThatWillBeSuccessful, [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(secondMessageIdThatWillFail, [], Encoding.UTF8.GetBytes("{}")),
                    new MulticastAddressTag(typeof(AnotherEvent)),
                    [],
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSnsClient.PublishedEvents, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.PublishedEvents.ElementAt(0).MessageAttributes[Headers.MessageId].StringValue, Is.EqualTo(firstMessageIdThatWillFail));
                Assert.That(mockSnsClient.PublishedEvents.ElementAt(1).MessageAttributes[Headers.MessageId].StringValue, Is.EqualTo(secondMessageIdThatWillFail));
            });
            Assert.That(mockSnsClient.BatchRequestsPublished, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(mockSnsClient.BatchRequestsPublished.ElementAt(0).PublishBatchRequestEntries.Select(x => x.MessageAttributes[Headers.MessageId].StringValue), Is.EquivalentTo(new[] { firstMessageIdThatWillFail, firstMessageThatWillBeSuccessful }));
                Assert.That(mockSnsClient.BatchRequestsPublished.ElementAt(1).PublishBatchRequestEntries.Select(x => x.MessageAttributes[Headers.MessageId].StringValue), Is.EquivalentTo(new[] { secondMessageIdThatWillFail, secondMessageThatWillBeSuccessful }));
            });
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_upload_large_non_isolated_operations_to_s3(bool wrapMessage)
        {
            var mockS3Client = new MockS3Client();
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null,
                new S3Settings("someBucket", "somePrefix", mockS3Client), 15 * 60, wrapOutgoingMessages: wrapMessage);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address1"),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Default),
                new TransportOperation( /* Crazy long message id will cause the message to go over limits because attributes count as well */
                    new OutgoingMessage(new string('x', 256 * 1024), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Default));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.BatchRequestsSent, Has.Count.EqualTo(3));
                Assert.That(mockS3Client.PutObjectRequestsSent, Has.Count.EqualTo(3));
            });

            var firstUpload = mockS3Client.PutObjectRequestsSent.ElementAt(0);
            var secondUpload = mockS3Client.PutObjectRequestsSent.ElementAt(1);
            var thirdUpload = mockS3Client.PutObjectRequestsSent.ElementAt(2);

            Assert.Multiple(() =>
            {
                Assert.That(firstUpload.BucketName, Is.EqualTo("someBucket"));
                Assert.That(secondUpload.BucketName, Is.EqualTo("someBucket"));
                Assert.That(thirdUpload.BucketName, Is.EqualTo("someBucket"));
            });
            if (wrapMessage)
            {
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.ElementAt(0).MessageBody, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{firstUpload.Key}"));
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.ElementAt(0).MessageBody, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{secondUpload.Key}"));
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(2).Entries.ElementAt(0).MessageBody, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{thirdUpload.Key}"));
            }
            else
            {
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.ElementAt(0).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(firstUpload.Key));
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.ElementAt(0).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(secondUpload.Key));
                Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(2).Entries.ElementAt(0).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(thirdUpload.Key));
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_upload_large_isolated_operations_request_to_s3(bool wrapMessage)
        {
            var mockS3Client = new MockS3Client();
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null,
                new S3Settings("someBucket", "somePrefix", mockS3Client), 15 * 60, wrapOutgoingMessages: wrapMessage);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address1"),
                    [],
                    DispatchConsistency.Isolated),
                new TransportOperation(
                    new OutgoingMessage(Guid.NewGuid().ToString(), [], Encoding.UTF8.GetBytes(new string('x', 256 * 1024))),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Isolated),
                new TransportOperation( /* Crazy long message id will cause the message to go over limits because attributes count as well */
                    new OutgoingMessage(new string('x', 256 * 1024), [], Encoding.UTF8.GetBytes("{}")),
                    new UnicastAddressTag("address2"),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSqsClient.RequestsSent.Count, Is.EqualTo(3));
            Assert.That(mockS3Client.PutObjectRequestsSent.Count, Is.EqualTo(3));

            var firstUpload = mockS3Client.PutObjectRequestsSent.ElementAt(0);
            var secondUpload = mockS3Client.PutObjectRequestsSent.ElementAt(1);
            var thirdUpload = mockS3Client.PutObjectRequestsSent.ElementAt(2);

            Assert.Multiple(() =>
            {
                Assert.That(firstUpload.BucketName, Is.EqualTo("someBucket"));
                Assert.That(secondUpload.BucketName, Is.EqualTo("someBucket"));
                Assert.That(thirdUpload.BucketName, Is.EqualTo("someBucket"));
            });
            if (wrapMessage)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(mockSqsClient.RequestsSent.ElementAt(0).MessageBody, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{firstUpload.Key}"));
                    Assert.That(mockSqsClient.RequestsSent.ElementAt(1).MessageBody, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{secondUpload.Key}"));
                    Assert.That(mockSqsClient.RequestsSent.ElementAt(2).MessageBody, Does.Contain($@"""Body"":"""",""S3BodyKey"":""{thirdUpload.Key}"));
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(mockSqsClient.RequestsSent.ElementAt(0).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(firstUpload.Key));
                    Assert.That(mockSqsClient.RequestsSent.ElementAt(1).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(secondUpload.Key));
                    Assert.That(mockSqsClient.RequestsSent.ElementAt(2).MessageAttributes[TransportHeaders.S3BodyKey].StringValue, Is.EqualTo(thirdUpload.Key));
                });
            }
        }

        [Test]
        public async Task Should_base64_encode_by_default()
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60);

            var msgBody = "my message body";
            var msgBodyByte = Encoding.UTF8.GetBytes(msgBody);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {TransportHeaders.TimeToBeReceived, ExpectedTtbr.ToString()},
                        {Headers.ReplyToAddress, ExpectedReplyToAddress}
                    }, msgBodyByte),
                    new UnicastAddressTag("address"),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSqsClient.RequestsSent, Is.Not.Empty, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            var bodyJson = JsonNode.Parse(request.MessageBody);

            Assert.That(bodyJson["Body"].GetValue<string>(), Is.EqualTo(Convert.ToBase64String(msgBodyByte)));
        }

        [Test]
        public async Task Should_not_wrap_if_configured_not_to()
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60, wrapOutgoingMessages: false);

            var msgBody = "my message body";
            var msgBodyByte = Encoding.UTF8.GetBytes(msgBody);

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage("1234", new Dictionary<string, string>
                    {
                        {TransportHeaders.TimeToBeReceived, ExpectedTtbr.ToString()},
                        {Headers.ReplyToAddress, ExpectedReplyToAddress},
                        {Headers.MessageId, "74d4f8e4-0fc7-4f09-8d46-0b76994e76d6"}
                    }, msgBodyByte),
                    new UnicastAddressTag("address"),
                    [],
                    DispatchConsistency.Isolated));

            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSqsClient.RequestsSent, Is.Not.Empty, "No requests sent");
            var request = mockSqsClient.RequestsSent.First();

            Approver.Verify(request);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_handle_empty_body_gracefully(bool wrapMessage)
        {
            var mockSqsClient = new MockSqsClient();

            var dispatcher = new MessageDispatcher(new SettingsHolder(), mockSqsClient, null, new QueueCache(mockSqsClient,
                dest => QueueCache.GetSqsQueueName(dest, "")), null, null, 15 * 60, wrapOutgoingMessages: wrapMessage);

            var messageid = Guid.NewGuid().ToString();

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(
                        messageid,
                        new Dictionary<string, string>
                        {
                            [Headers.MessageId] = messageid
                        },
                        Array.Empty<byte>()
                        ),
                    new UnicastAddressTag("Somewhere"))
                );
            var transportTransaction = new TransportTransaction();

            await dispatcher.Dispatch(transportOperations, transportTransaction);

            Assert.That(mockSqsClient.BatchRequestsSent, Is.Not.Empty, "No requests sent");
            var batch = mockSqsClient.BatchRequestsSent.First();
            var request = batch.Entries.First();

            Assert.That(request.MessageBody, Is.Not.Null.Or.Empty);
        }

        interface IEvent { }

        interface IMyEvent : IEvent { }
        class Event : IMyEvent { }
        class AnotherEvent : IMyEvent { }
    }
}