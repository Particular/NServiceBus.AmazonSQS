namespace NServiceBus.Transport.SQS.Tests
{
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
    using Particular.Approvals;
    using Settings;
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

            transportSettings = new TransportExtensions<SqsTransport>(settings);
        }

        TestableSubscriptionManager CreateNonBatchingSubscriptionManager()
        {
            settings.Set(SettingsKeys.DisableSubscribeBatchingOnStart, true);

            var transportConfiguration = new TransportConfiguration(settings);

            return new TestableSubscriptionManager(
                transportConfiguration,
                sqsClient,
                snsClient,
                queueName,
                new QueueCache(sqsClient,
                    new TransportConfiguration(settings)),
                messageMetadataRegistry,
                new TopicCache(snsClient,
                    messageMetadataRegistry,
                    new TransportConfiguration(settings))
                );
        }

        TestableSubscriptionManager CreateBatchingSubscriptionManager()
        {
            settings.Set(SettingsKeys.DisableSubscribeBatchingOnStart, false);

            var transportConfiguration = new TransportConfiguration(settings);

            return new TestableSubscriptionManager(
                transportConfiguration,
                sqsClient,
                snsClient,
                queueName,
                new QueueCache(sqsClient,
                    new TransportConfiguration(settings)),
                messageMetadataRegistry,
                new TopicCache(snsClient,
                    messageMetadataRegistry,
                    new TransportConfiguration(settings))
                );
        }

        [Test]
        public async Task Subscribe_object_should_work()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(object);

            await manager.Subscribe(eventType, null);

            Assert.IsNotEmpty(snsClient.SubscribeRequestsSent);
        }

        [Test]
        public async Task Subscribe_again_should_ignore_because_cached()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);
            await manager.Unsubscribe(eventType, null);

            await manager.Subscribe(eventType, null);

            Assert.AreEqual(2, snsClient.SubscribeRequestsSent.Count);
        }

        [Test]
        public async Task Subscribe_always_creates_topic()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<string> {"NServiceBus-Transport-SQS-Tests-SubscriptionManagerTests-Event"}, snsClient.CreateTopicRequests);
            Assert.IsEmpty(snsClient.FindTopicRequests);
        }

        [Test]
        public async Task Subscribe_with_event_to_topics_mapping_creates_custom_defined_topic()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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
            var manager = CreateNonBatchingSubscriptionManager();

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

            await manager.Subscribe(subscribedEventType, null);

            Assert.AreEqual(1, sqsClient.SetAttributesRequestsSent.Count);
        }

        [Test]
        public async Task Subscribe_creates_subscription_with_raw_message_mode()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);

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

            Assert.DoesNotThrowAsync(async () => await manager.Subscribe(typeof(Event), null));
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

            Assert.DoesNotThrowAsync(async () => await manager.Subscribe(typeof(Event), null));
            Assert.AreEqual(7, manager.Delays.Count);
            Assert.AreEqual(35000, manager.Delays.Sum());
        }

        [Test]
        public async Task Subscribe_endpointstarting_creates_topic_but_not_policies_and_subscriptions()
        {
            var manager = CreateBatchingSubscriptionManager();

            var eventType = typeof(Event);
            await manager.Subscribe(eventType, null);

            Assert.AreEqual(1, snsClient.CreateTopicRequests.Count);
            Assert.IsEmpty(snsClient.SubscribeRequestsSent);
            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        [Test]
        public async Task SettlePolicy_sets_full_policy()
        {
            var manager = CreateBatchingSubscriptionManager();

            var eventType = typeof(Event);
            await manager.Subscribe(eventType, null);
            var anotherEvent = typeof(AnotherEvent);
            await manager.Subscribe(anotherEvent, null);

            var setAttributeRequestsSentBeforeSettle = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            await manager.Settle();

            Assert.IsEmpty(setAttributeRequestsSentBeforeSettle);
            Approver.Verify(sqsClient.SetAttributesRequestsSent[0].attributes["Policy"], PolicyScrubber.ScrubPolicy);
        }

        [Test]
        public async Task SettlePolicy_with_all_conditions_sets_full_policy()
        {
            transportSettings.TopicNamePrefix("DEV-");

            var policies = transportSettings.Policies();
            policies.AddAccountCondition();
            policies.AddTopicNamePrefixCondition();
            policies.AddNamespaceCondition("Shipping.");
            policies.AddNamespaceCondition("Sales.HighValueOrders.");

            var manager = CreateBatchingSubscriptionManager();

            var eventType = typeof(Event);
            await manager.Subscribe(eventType, null);
            var anotherEvent = typeof(AnotherEvent);
            await manager.Subscribe(anotherEvent, null);

            var setAttributeRequestsSentBeforeSettle = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            await manager.Settle();

            Assert.IsEmpty(setAttributeRequestsSentBeforeSettle);
            Approver.Verify(sqsClient.SetAttributesRequestsSent[0].attributes["Policy"], PolicyScrubber.ScrubPolicy);
        }

        [Test]
        public async Task SettlePolicy_with_nothing_to_subscribe_and_no_policy_doesnt_create_policy()
        {
            var manager = CreateBatchingSubscriptionManager();

#pragma warning disable 618
            var existingPolicy = new Policy();
#pragma warning restore 618

            EmulateImmediateSettlementOfPolicy(existingPolicy);

            var setAttributeRequestsSentBeforeSettle = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            await manager.Settle();

            Assert.IsEmpty(setAttributeRequestsSentBeforeSettle);
            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        [Test]
        public async Task SettlePolicy_with_policy_assumed_to_have_permissions_doesnt_acquire_policy_but_creates_topics_and_subscriptions()
        {
            var policies = transportSettings.Policies();
            policies.AssumePolicyHasAppropriatePermissions();

            var manager = CreateBatchingSubscriptionManager();

            var eventType = typeof(Event);
            await manager.Subscribe(eventType, null);
            var anotherEvent = typeof(AnotherEvent);
            await manager.Subscribe(anotherEvent, null);

            var setAttributeRequestsSentBeforeSettle = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            await manager.Settle();

            Assert.IsEmpty(setAttributeRequestsSentBeforeSettle);
            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
            Assert.AreEqual(1, sqsClient.GetAttributeRequestsSent.Count, "One queue attribute get request should have been sent only.");
            Assert.AreEqual(2, snsClient.SubscribeRequestsSent.Count, "A subscribe request per topic should have been sent");
            Assert.AreEqual(2, snsClient.CreateTopicRequests.Count, "A create topic request per topic should have been sent");
        }

        [Test]
        public async Task After_settle_policy_does_not_batch_subscriptions()
        {
            var manager = CreateBatchingSubscriptionManager();

            await manager.Subscribe(typeof(Event), null);
            await manager.Subscribe(typeof(AnotherEvent), null);

            var setAttributeRequestsSentBeforeSettle = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            await manager.Settle();

            var setAttributeRequestsSentAfterSettle = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            await manager.Subscribe(typeof(YetAnotherEvent), null);
            await manager.Subscribe(typeof(object), null);

            var setAttributeRequestsSentAfterSubscribe = new List<(string queueUrl, Dictionary<string, string> attributes)>(sqsClient.SetAttributesRequestsSent);

            Assert.IsEmpty(setAttributeRequestsSentBeforeSettle);
            Assert.AreEqual(1, setAttributeRequestsSentAfterSettle.Count);
            Assert.AreEqual(3, setAttributeRequestsSentAfterSubscribe.Count);
        }

        [Test]
        public async Task Unsubscribe_object_should_ignore()
        {
            var manager = CreateNonBatchingSubscriptionManager();

            var eventType = typeof(object);

            await manager.Unsubscribe(eventType, null);

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

            await manager.Unsubscribe(eventType, null);

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

            await manager.Unsubscribe(eventType, null);

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

            await manager.Unsubscribe(eventType, null);

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

            await manager.Unsubscribe(unsubscribedEvent, null);

            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

        [Test]
        public async Task Unsubscribe_with_event_to_topics_mapping_should_unsubscribe_matching_subscription()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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

        [Test]
        public async Task Unsubscribe_with_event_to_topics_mapping_should_not_modify_policy()
        {
            var manager = CreateNonBatchingSubscriptionManager();

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

            Assert.IsEmpty(sqsClient.SetAttributesRequestsSent);
        }

#pragma warning disable 618
        private void EmulateImmediateSettlementOfPolicy(Policy initialPolicy)
#pragma warning restore 618
        {
            var invocationCount = 0;
            var original = sqsClient.GetAttributeRequestsResponse;
            sqsClient.GetAttributeRequestsResponse = s =>
            {
                invocationCount++;

                // three calls because one from the QueueCache and two from the settlement
                if (invocationCount < 3)
                {
                    return new Dictionary<string, string>
                    {
                        {"QueueArn", "arn:fakeQueue"},
                        {"Policy", initialPolicy.ToJson()}
                    };
                }

                return original(s);
            };
        }

        MockSqsClient sqsClient;
        MockSnsClient snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        SettingsHolder settings;
        string queueName;
        EventToTopicsMappings customEventToTopicsMappings;
        EventToEventsMappings customEventToEventsMappings;
        TransportExtensions<SqsTransport> transportSettings;

        class TestableSubscriptionManager : SubscriptionManager
        {
            public TestableSubscriptionManager(TransportConfiguration configuration, IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, string queueName, QueueCache queueCache, MessageMetadataRegistry messageMetadataRegistry, TopicCache topicCache) : base(configuration, sqsClient, snsClient, queueName, queueCache, messageMetadataRegistry, topicCache)
            {
            }

            public List<int> Delays { get; } = new List<int>();

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

        class YetAnotherEvent : IMyEvent
        {
        }

        class YetYetAnotherEvent : IMyEvent
        {
        }
    }
}