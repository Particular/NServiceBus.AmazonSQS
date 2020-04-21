namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Configure;
    using NUnit.Framework;
    using Settings;
    using SQS;
    using Unicast.Messages;

    [TestFixture]
    public class SubscriptionManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            sqsClient = new MockSqsClient();
            snsClient = new MockSnsClient();
            sqsClient.EnableGetAttributeReturnsWhatWasSet();
            settings = new SettingsHolder();

            customEventToTopicsMappings = new EventToTopicsMappings();
            settings.Set(customEventToTopicsMappings);

            customEventToEventsMappings = new EventToEventsMappings();
            settings.Set(customEventToEventsMappings);

            messageMetadataRegistry = settings.SetupMessageMetadataRegistry();
            queueName = "fakeQueue";

            var transportConfiguration = new TransportConfiguration(settings);
            manager = new TestableSubscriptionManager(sqsClient, snsClient, queueName, new QueueCache(sqsClient, transportConfiguration), messageMetadataRegistry, new TopicCache(snsClient, messageMetadataRegistry, transportConfiguration));
        }

        [Test]
        public async Task Subscribe_object_should_ignore()
        {
            var eventType = typeof(object);

            await manager.Subscribe(eventType, null);

            Assert.IsEmpty(snsClient.SubscribeQueueRequests);
        }

        [Test]
        public async Task Subscribe_again_should_ignore_because_cached()
        {
            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);

            var initialSubscribeRequests = new List<SubscribeRequest>(snsClient.SubscribeRequestsSent);
            snsClient.SubscribeRequestsSent.Clear();

            await manager.Subscribe(eventType, null);

            Assert.IsNotEmpty(initialSubscribeRequests);
            Assert.IsEmpty(snsClient.SubscribeRequestsSent);
        }

        [Test]
        public async Task Subscribe_Unsubscribe_and_Subscribe_again()
        {
            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);
            await manager.Unsubscribe(eventType, null);

            await manager.Subscribe(eventType, null);

            Assert.AreEqual(2, snsClient.SubscribeRequestsSent.Count);
        }

        [Test]
        public async Task Subscribe_always_creates_topic()
        {
            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<string> {"NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event"}, snsClient.CreateTopicRequests);
            Assert.IsEmpty(snsClient.FindTopicRequests);
        }

        [Test]
        public async Task Subscribe_with_event_to_topics_mapping_creates_custom_defined_topic()
        {
            var eventType = typeof(Event);
            customEventToTopicsMappings.Add(eventType, new[] {"custom-topic-name"});

            await manager.Subscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "custom-topic-name",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event"
            }, snsClient.CreateTopicRequests);
            Assert.IsEmpty(snsClient.FindTopicRequests);
        }

        [Test]
        public async Task Subscribe_with_event_to_events_mapping_creates_custom_defined_topic()
        {
            var subscribedEventType = typeof(IEvent);
            var concreteEventType = typeof(Event);
            var concreteAnotherEventType = typeof(AnotherEvent);
            customEventToEventsMappings.Add(subscribedEventType, concreteEventType);
            customEventToEventsMappings.Add(subscribedEventType, concreteAnotherEventType);

            await manager.Subscribe(subscribedEventType, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-IEvent"
            }, snsClient.CreateTopicRequests);
            Assert.IsEmpty(snsClient.FindTopicRequests);
        }

        [Test]
        public async Task Subscribe_creates_subscription_with_raw_message_mode()
        {
            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);

            Assert.AreEqual(1, snsClient.SubscribeRequestsSent.Count);
            var subscribeRequest = snsClient.SubscribeRequestsSent[0];
            Assert.AreEqual("arn:fakeQueue", subscribeRequest.Endpoint);
            Assert.AreEqual("arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", subscribeRequest.TopicArn);
            Assert.IsTrue(subscribeRequest.ReturnSubscriptionArn);

            Assert.AreEqual(1, snsClient.SetSubscriptionAttributesRequests.Count);
            var setAttributeRequest = snsClient.SetSubscriptionAttributesRequests[0];
            Assert.AreEqual("RawMessageDelivery", setAttributeRequest.AttributeName);
            Assert.AreEqual("true", setAttributeRequest.AttributeValue);
            Assert.AreEqual("arn:fakeQueue", setAttributeRequest.SubscriptionArn);
        }

        [Test]
        public void Subscribe_retries_setting_raw_Mode_five_times_with_linear_delays_and_gives_up()
        {
            snsClient.SetSubscriptionAttributesResponse = req => throw new NotFoundException("");

            Assert.ThrowsAsync<NotFoundException>(async() => await manager.Subscribe(typeof(Event), null));
            Assert.AreEqual(5, manager.Delays.Count);
            Assert.AreEqual(15000, manager.Delays.Sum());
        }

        [Test]
        public void Subscribe_retries_setting_raw_Mode_with_linear_delays()
        {
            var queue = new Queue<Func<SetSubscriptionAttributesResponse>>();
            queue.Enqueue(() => throw new NotFoundException(""));
            queue.Enqueue(() => throw new NotFoundException(""));
            queue.Enqueue(() => throw new NotFoundException(""));
            queue.Enqueue(() => throw new NotFoundException(""));
            queue.Enqueue(() => new SetSubscriptionAttributesResponse());
            snsClient.SetSubscriptionAttributesResponse = req => queue.Dequeue()();

            Assert.DoesNotThrowAsync(async() => await manager.Subscribe(typeof(Event), null));
            Assert.AreEqual(4, manager.Delays.Count);
            Assert.AreEqual(10000, manager.Delays.Sum());
        }

        [Test]
        public void Subscribe_retries_setting_policies_eight_times_with_linear_delays_and_gives_up()
        {
            sqsClient.GetAttributeRequestsResponse = s => new Dictionary<string, string>
            {
                { "QueueArn", "arn:fakeQueue" }
            };

            Assert.DoesNotThrowAsync(async() => await manager.Subscribe(typeof(Event), null));
            Assert.AreEqual(8, manager.Delays.Count);
            Assert.AreEqual(44000, manager.Delays.Sum());
        }

        [Test]
        public void Subscribe_retries_setting_policies_seven_times_with_linear_delays()
        {
            var invocationCount = 0;
            var original = sqsClient.GetAttributeRequestsResponse;
            sqsClient.GetAttributeRequestsResponse = s =>
            {
                invocationCount++;
                if (invocationCount < 10)
                {
                    return new Dictionary<string, string>
                    {
                        {"QueueArn", "arn:fakeQueue"}
                    };
                }
                return original(s);
            };

            Assert.DoesNotThrowAsync(async() => await manager.Subscribe(typeof(Event), null));
            Assert.AreEqual(7, manager.Delays.Count);
            Assert.AreEqual(35000, manager.Delays.Sum());
        }

        [Test]
        public async Task Subscribe_endpointstarting_creates_topic_but_not_policies_and_subscriptions()
        {
            manager.EndpointStartingMode = true;

            var eventType = typeof(Event);
            await manager.Subscribe(eventType, null);

            Assert.AreEqual(1, snsClient.CreateTopicRequests.Count);
            Assert.IsEmpty(snsClient.SubscribeRequestsSent);
            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        [Test]
        public async Task SettlePolicy_sets_full_policy()
        {
            manager.EndpointStartingMode = true;

            var eventType = typeof(Event);
            await manager.Subscribe(eventType, null);
            var anotherEvent = typeof(AnotherEvent);
            await manager.Subscribe(anotherEvent, null);

            await manager.Settle();

            var policy = Policy.FromJson(sqsClient.SetAttributesRequestsSent[0].attributes["Policy"]);

            Assert.AreEqual(2, policy.Statements.Count);
            CollectionAssert.AreEquivalent(new []
            {
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent"
            }, policy.Statements.SelectMany(s => s.Conditions).SelectMany(c => c.Values));
            Assert.IsFalse(manager.EndpointStartingMode);
        }

        [Test]
        public async Task Unsubscribe_object_should_ignore()
        {
            var eventType = typeof(object);

            await manager.Unsubscribe(eventType, null);

            Assert.IsEmpty(snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_if_no_subscription_doesnt_unsubscribe()
        {
            snsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions = new List<Subscription>
                {
                    new Subscription {Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription"},
                    new Subscription {Endpoint = "arn:yetAnotherQueue", SubscriptionArn = "arn:yetAnotherSubscription"}
                }
            };

            var eventType = typeof(Event);

            await manager.Unsubscribe(eventType, null);

            Assert.IsEmpty(snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_should_unsubscribe_matching_subscription()
        {
            snsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions = new List<Subscription>
                {
                    new Subscription {Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription"},
                    new Subscription {Endpoint = $"arn:{queueName}", SubscriptionArn = "arn:subscription"}
                }
            };

            var eventType = typeof(Event);

            await manager.Unsubscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "arn:subscription"
            }, snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_with_events_to_events_mapping_should_unsubscribe_matching_subscription()
        {
            var unsubscribedEvent = typeof(IEvent);
            var concreteEventType = typeof(Event);
            customEventToEventsMappings.Add(unsubscribedEvent, concreteEventType);

            snsClient.ListSubscriptionsByTopicResponse = topic =>
            {
                if (topic.EndsWith("NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event"))
                {
                    return new ListSubscriptionsByTopicResponse
                    {
                        Subscriptions = new List<Subscription>
                        {
                            new Subscription {Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription"},
                            new Subscription {Endpoint = $"arn:{queueName}", SubscriptionArn = "arn:subscription"}
                        }
                    };
                }

                return new ListSubscriptionsByTopicResponse
                {
                    Subscriptions = new List<Subscription>()
                };
            };

            await manager.Unsubscribe(unsubscribedEvent, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-IEvent"
            }, snsClient.FindTopicRequests);
            CollectionAssert.AreEquivalent(new List<string>
            {
                "arn:subscription"
            }, snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_with_event_to_topics_mapping_should_unsubscribe_matching_subscription()
        {
            var unsubscribedEvent = typeof(IEvent);
            customEventToTopicsMappings.Add(unsubscribedEvent, new[] {"custom-topic-name"});

            snsClient.ListSubscriptionsByTopicResponse = topic =>
            {
                if (topic.EndsWith("custom-topic-name"))
                {
                    return new ListSubscriptionsByTopicResponse
                    {
                        Subscriptions = new List<Subscription>
                        {
                            new Subscription {Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription"},
                            new Subscription {Endpoint = $"arn:{queueName}", SubscriptionArn = "arn:subscription"}
                        }
                    };
                }

                return new ListSubscriptionsByTopicResponse
                {
                    Subscriptions = new List<Subscription>()
                };
            };

            await manager.Unsubscribe(unsubscribedEvent, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "custom-topic-name",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-IEvent"
            }, snsClient.FindTopicRequests);
            CollectionAssert.AreEquivalent(new List<string>
            {
                "arn:subscription"
            }, snsClient.UnsubscribeRequests);
        }

        MockSqsClient sqsClient;
        TestableSubscriptionManager manager;
        MockSnsClient snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        SettingsHolder settings;
        string queueName;
        EventToTopicsMappings customEventToTopicsMappings;
        EventToEventsMappings customEventToEventsMappings;

        class TestableSubscriptionManager : SubscriptionManager
        {
            public TestableSubscriptionManager(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueCache queueCache, MessageMetadataRegistry messageMetadataRegistry, TopicCache topicCache) : base(sqsClient, snsClient, queueName, queueCache, messageMetadataRegistry, topicCache)
            {
                EndpointStartingMode = false;
            }

            public List<int> Delays { get; } = new List<int>();

            public bool EndpointStartingMode
            {
                set => endpointStartingMode = value;
                get => endpointStartingMode;
            }

            protected override Task Delay(int millisecondsDelay, CancellationToken token = default)
            {
                Delays.Add(millisecondsDelay);
                return Task.FromResult(0);
            }
        }

        interface IEvent
        {
        }

        interface IMyEvent : IEvent
        {
        }

        class Event : IMyEvent
        {
        }

        class AnotherEvent : IMyEvent
        {
        }
    }
}