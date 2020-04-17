namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.Runtime.SharedInterfaces;
    using Amazon.SimpleNotificationService.Model;
    using NUnit.Framework;
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
            settings = new SettingsHolder();

            customEventToTopicsMappings = new EventToTopicsMappings();
            settings.Set(customEventToTopicsMappings);

            customEventToEventsMappings = new EventToEventsMappings();
            settings.Set(customEventToEventsMappings);

            messageMetadataRegistry = settings.SetupMessageMetadataRegistry();
            queueName = "fakeQueue";

            var transportConfiguration = new TransportConfiguration(settings);
            manager = new SubscriptionManager(sqsClient, snsClient, queueName, new QueueCache(sqsClient, transportConfiguration), messageMetadataRegistry, new TopicCache(snsClient, messageMetadataRegistry, transportConfiguration));
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

            var initialSubscribeRequests = new List<(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)>(snsClient.SubscribeQueueRequests);
            snsClient.SubscribeQueueRequests.Clear();

            await manager.Subscribe(eventType, null);

            Assert.IsNotEmpty(initialSubscribeRequests);
            Assert.IsEmpty(snsClient.SubscribeQueueRequests);
        }

        [Test]
        public async Task Subscribe_Unsubscribe_and_Subscribe_again()
        {
            var eventType = typeof(Event);

            await manager.Subscribe(eventType, null);
            await manager.Unsubscribe(eventType, null);

            await manager.Subscribe(eventType, null);

            Assert.AreEqual(2, snsClient.SubscribeQueueRequests.Count);
        }

        [Test]
        public async Task Subscribe_creates_topic_if_not_exists()
        {
            var eventType = typeof(Event);

            var responses = new Queue<Func<string, Topic>>();
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            snsClient.FindTopicAsyncResponse = topic => responses.Dequeue()(topic);

            await manager.Subscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<string> { "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event" }, snsClient.CreateTopicRequests);
        }

        [Test]
        public async Task Subscribe_with_event_to_topics_mapping_creates_custom_defined_topic_if_not_exists()
        {
            var eventType = typeof(Event);
            customEventToTopicsMappings.Add(eventType, new []{"custom-topic-name"});

            var responses = new Queue<Func<string, Topic>>();
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            snsClient.FindTopicAsyncResponse = topic => responses.Dequeue()(topic);

            await manager.Subscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "custom-topic-name",
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event"
            }, snsClient.CreateTopicRequests);
        }

        [Test]
        public async Task Subscribe_with_event_to_events_mapping_creates_custom_defined_topic_if_not_exists()
        {
            var subscribedEventType = typeof(IEvent);
            var concreteEventType = typeof(Event);
            var concreteAnotherEventType = typeof(AnotherEvent);
            customEventToEventsMappings.Add(subscribedEventType, concreteEventType);
            customEventToEventsMappings.Add(subscribedEventType, concreteAnotherEventType);

            var responses = new Queue<Func<string, Topic>>();
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            snsClient.FindTopicAsyncResponse = topic => responses.Dequeue()(topic);

            await manager.Subscribe(subscribedEventType, null);

            CollectionAssert.AreEquivalent(new List<string>
            {
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event",
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-AnotherEvent",
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-IEvent"
            }, snsClient.CreateTopicRequests);
        }

        // Apparently we can only set the raw mode by doing it that way so let's enforce via test
        [Test]
        public async Task Subscribe_creates_subscription_with_passed_sqs_client_and_raw_message()
        {
            var eventType = typeof(Event);
            messageMetadataRegistry.GetMessageMetadata(eventType);

            await manager.Subscribe(eventType, null);

            CollectionAssert.AreEquivalent(new List<(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)>
            {
                ("arn:aws:sns:us-west-2:123456789012:NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event", sqsClient, queueName)
            }, snsClient.SubscribeQueueRequests);

            Assert.AreEqual(1, snsClient.SetSubscriptionAttributesRequests.Count);
            var firstElement = snsClient.SetSubscriptionAttributesRequests[0];
            Assert.AreEqual("RawMessageDelivery", firstElement.AttributeName);
            Assert.AreEqual("true", firstElement.AttributeValue);
            Assert.AreEqual("arn:aws:sns:us-west-2:123456789012:arn:aws:sns:us-west-2:123456789012:NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event:6b0e71bd-7e97-4d97-80ce-4a0994e55286", firstElement.SubscriptionArn);
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
                    new Subscription { Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription" },
                    new Subscription { Endpoint = "arn:yetAnotherQueue", SubscriptionArn = "arn:yetAnotherSubscription" }
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
                    new Subscription { Endpoint = "arn:someOtherQueue", SubscriptionArn = "arn:someOtherSubscription" },
                    new Subscription { Endpoint = $"arn:{queueName}", SubscriptionArn = "arn:subscription" }
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
                if (topic.EndsWith("NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event"))
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
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-Event",
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-IEvent"
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
            customEventToTopicsMappings.Add(unsubscribedEvent, new []{"custom-topic-name"});

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
                "NServiceBus-AmazonSQS-Tests-SubscriptionManagerTests-IEvent"
            }, snsClient.FindTopicRequests);
            CollectionAssert.AreEquivalent(new List<string>
            {
                "arn:subscription"
            }, snsClient.UnsubscribeRequests);
        }

        interface IEvent { }

        interface IMyEvent : IEvent { }
        class Event : IMyEvent { }

        class AnotherEvent : IMyEvent { }

        MockSqsClient sqsClient;
        SubscriptionManager manager;
        MockSnsClient snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        SettingsHolder settings;
        string queueName;
        EventToTopicsMappings customEventToTopicsMappings;
        EventToEventsMappings customEventToEventsMappings;
    }
}