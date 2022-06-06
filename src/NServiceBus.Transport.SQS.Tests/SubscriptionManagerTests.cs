namespace NServiceBus.Transport.SQS.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using Configure;
    using NUnit.Framework;
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

            customEventToTopicsMappings = new EventToTopicsMappings();
            customEventToEventsMappings = new EventToEventsMappings();
            policySettings = new PolicySettings();

            queueName = "fakeQueue";
        }

        TestableSubscriptionManager CreateNonBatchingSubscriptionManager()
        {
            return new TestableSubscriptionManager(
                sqsClient,
                snsClient,
                queueName,
                new QueueCache(sqsClient, dest => QueueCache.GetSqsQueueName(dest, "")),
                new TopicCache(snsClient, new Settings.SettingsHolder(), customEventToTopicsMappings, customEventToEventsMappings,
                    TopicNameHelper.GetSnsTopicName, ""),
                policySettings,
                "");
        }

        [Test]
        public async Task Subscribe_object_should_work()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(object);

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

            Assert.IsNotEmpty(snsClient.SubscribeRequestsSent);
        }

        [Test]
        public async Task Subscribe_again_should_ignore_because_cached()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

            var initialSubscribeRequests = new List<SubscribeRequest>(snsClient.SubscribeRequestsSent);
            snsClient.SubscribeRequestsSent.Clear();

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

            Assert.IsNotEmpty(initialSubscribeRequests);
            Assert.IsEmpty(snsClient.SubscribeRequestsSent);
        }

        [Test]
        public async Task Subscribe_Unsubscribe_and_Subscribe_again()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);
            await manager.Unsubscribe(new MessageMetadata(eventType), null);

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

            Assert.AreEqual(2, snsClient.SubscribeRequestsSent.Count);
        }

        [Test]
        public async Task Subscribe_always_creates_topic()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

            CollectionAssert.AreEquivalent(new List<string> { "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event" }, snsClient.CreateTopicRequests);
            Assert.IsEmpty(snsClient.FindTopicRequests);
        }

        [Test]
        public async Task Subscribe_with_event_to_topics_mapping_creates_custom_defined_topic()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);
            customEventToTopicsMappings.Add(eventType, new[] { "custom-topic-name" });

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

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
            var manager = CreateNonBatchingSubscriptionManager();

            var subscribedEventType = typeof(IEvent);
            var concreteEventType = typeof(Event);
            var concreteAnotherEventType = typeof(AnotherEvent);
            customEventToEventsMappings.Add(subscribedEventType, concreteEventType);
            customEventToEventsMappings.Add(subscribedEventType, concreteAnotherEventType);

            await manager.SubscribeAll(new[] { new MessageMetadata(subscribedEventType) }, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-AnotherEvent",
                "NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-IEvent"
            }, snsClient.CreateTopicRequests);
            Assert.IsEmpty(snsClient.FindTopicRequests);
        }

        // it is crucial to settle one consistent topology for all the topics determined when mapping is at play to avoid
        // running into eventual consistency problems and eventually create a policy that is partial/malformed causing
        // subscriptions to not work. This problem was seen by the flakiness of When_multiple_versions_of_a_message_is_published
        [Test]
        public async Task Subscribe_with_event_to_events_mapping_settles_policy_once_instead_of_for_all_topics()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var subscribedEventType = typeof(IEvent);
            var concreteEventType = typeof(Event);
            var concreteAnotherEventType = typeof(AnotherEvent);
            customEventToEventsMappings.Add(subscribedEventType, concreteEventType);
            customEventToEventsMappings.Add(subscribedEventType, concreteAnotherEventType);

            await manager.SubscribeAll(new[] { new MessageMetadata(subscribedEventType) }, null);

            Assert.AreEqual(1, sqsClient.SetAttributesRequestsSent.Count);
        }

        [Test]
        public async Task Subscribe_creates_subscription_with_raw_message_mode()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.SubscribeAll(new[] { new MessageMetadata(eventType) }, null);

            Assert.AreEqual(1, snsClient.SubscribeRequestsSent.Count);
            var subscribeRequest = snsClient.SubscribeRequestsSent[0];
            Assert.AreEqual("arn:fakeQueue", subscribeRequest.Endpoint);
            Assert.AreEqual("arn:aws:sns:us-west-2:123456789012:NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event", subscribeRequest.TopicArn);
            CollectionAssert.AreEquivalent(new Dictionary<string, string>
            {
                {"RawMessageDelivery", "true"}
            }, subscribeRequest.Attributes);
        }

        [Test]
        public void Subscribe_retries_setting_policies_eight_times_with_linear_delays_and_gives_up()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            sqsClient.GetAttributeRequestsResponse = s => new Dictionary<string, string>
            {
                {"QueueArn", "arn:fakeQueue"}
            };

            Assert.DoesNotThrowAsync(async () => await manager.SubscribeAll(new[] { new MessageMetadata(typeof(Event)) }, null));
            Assert.AreEqual(8, manager.Delays.Count);
            Assert.AreEqual(44000, manager.Delays.Sum());
        }

        [Test]
        public void Subscribe_retries_setting_policies_seven_times_with_linear_delays()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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

            Assert.DoesNotThrowAsync(async () => await manager.SubscribeAll(new[] { new MessageMetadata(typeof(Event)) }, null));
            Assert.AreEqual(7, manager.Delays.Count);
            Assert.AreEqual(35000, manager.Delays.Sum());
        }

        [Test]
        public async Task Unsubscribe_object_should_ignore()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(object);

            await manager.Unsubscribe(new MessageMetadata(eventType), null);

            Assert.IsEmpty(snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_if_no_subscription_doesnt_unsubscribe()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            snsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions = new List<Subscription>
                {
                    new Subscription {Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription"},
                    new Subscription {Endpoint = "arn:yetAnotherQueue", SubscriptionArn = "arn:yetAnotherSubscription"}
                }
            };

            var eventType = typeof(Event);

            await manager.Unsubscribe(new MessageMetadata(eventType), null);

            Assert.IsEmpty(snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_should_unsubscribe_matching_subscription()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            snsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions = new List<Subscription>
                {
                    new Subscription {Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription"},
                    new Subscription {Endpoint = $"arn:{queueName}", SubscriptionArn = "arn:subscription"}
                }
            };

            var eventType = typeof(Event);

            await manager.Unsubscribe(new MessageMetadata(eventType), null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "arn:subscription"
            }, snsClient.UnsubscribeRequests);
        }

        [Test]
        public async Task Unsubscribe_should_not_modify_policy()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            snsClient.ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
            {
                Subscriptions = new List<Subscription>
                {
                    new Subscription {Endpoint = $"arn:{queueName}", SubscriptionArn = "arn:subscription"}
                }
            };

            var eventType = typeof(Event);

            await manager.Unsubscribe(new MessageMetadata(eventType), null);

            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        [Test]
        public async Task Unsubscribe_with_events_to_events_mapping_should_unsubscribe_matching_subscription()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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

            await manager.Unsubscribe(new MessageMetadata(unsubscribedEvent), null);

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
        public async Task Unsubscribe_with_events_to_events_mapping_should_not_modify_policy()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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

            await manager.Unsubscribe(new MessageMetadata(unsubscribedEvent), null);

            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        [Test]
        public async Task Unsubscribe_with_event_to_topics_mapping_should_unsubscribe_matching_subscription()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var unsubscribedEvent = typeof(IEvent);
            customEventToTopicsMappings.Add(unsubscribedEvent, new[] { "custom-topic-name" });

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

            await manager.Unsubscribe(new MessageMetadata(unsubscribedEvent), null);

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

        [Test]
        public async Task Unsubscribe_with_event_to_topics_mapping_should_not_modify_policy()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var unsubscribedEvent = typeof(IEvent);
            customEventToTopicsMappings.Add(unsubscribedEvent, new[] { "custom-topic-name" });

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

            await manager.Unsubscribe(new MessageMetadata(unsubscribedEvent), null);

            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        MockSqsClient sqsClient;
        MockSnsClient snsClient;
        PolicySettings policySettings;
        string queueName;
        EventToTopicsMappings customEventToTopicsMappings;
        EventToEventsMappings customEventToEventsMappings;

        class TestableSubscriptionManager : SubscriptionManager
        {
            public TestableSubscriptionManager(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueCache queueCache, TopicCache topicCache, PolicySettings policySettings, string topicNamePrefix)
                : base(sqsClient, snsClient, queueName, queueCache, topicCache, policySettings, topicNamePrefix)
            {
            }

            public List<int> Delays { get; } = new List<int>();

            protected override Task Delay(int millisecondsDelay, CancellationToken cancellationToken = default)
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

        class YetAnotherEvent : IMyEvent
        {
        }

        class YetYetAnotherEvent : IMyEvent
        {
        }
    }
}