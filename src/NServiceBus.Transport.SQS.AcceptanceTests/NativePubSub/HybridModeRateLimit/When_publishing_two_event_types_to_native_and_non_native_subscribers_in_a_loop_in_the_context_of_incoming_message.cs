namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_two_event_types_to_native_and_non_native_subscribers_in_a_loop_in_the_context_of_incoming_message : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases = new TestCase[]
        {
            new TestCase(1){ NumberOfEvents = 1 },
            new TestCase(2){ NumberOfEvents = 100, MessageVisibilityTimeout = 60, },
            new TestCase(3)
            {
                NumberOfEvents = 200,
                MessageVisibilityTimeout = 120,
                SubscriptionsCacheTTL = TimeSpan.FromMinutes(2),
                NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
            },
            new TestCase(4)
            {
                NumberOfEvents = 300,
                MessageVisibilityTimeout = 180,
                TestExecutionTimeout = TimeSpan.FromMinutes(3),
                SubscriptionsCacheTTL = TimeSpan.FromMinutes(2),
                NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
            },
            new TestCase(5)
            {
                NumberOfEvents = 1000,
                MessageVisibilityTimeout = 360,
                SubscriptionsCacheTTL = TimeSpan.FromSeconds(120),
                TestExecutionTimeout = TimeSpan.FromMinutes(8),
                NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
            },
        };

        [Test, TestCaseSource(nameof(TestCases))]
        public async Task Should_not_rate_exceed(TestCase testCase)
        {
            if (testCase.Sequence.HasValue)
            {
                SetupFixture.AppendSequenceToCustomNamePrefix(testCase.Sequence.Value);
            }

            var context = await Scenario.Define<Context>()
                .WithEndpoint<MessageDrivenPubSubSubscriber>(b =>
                {
                    b.When(async (session, ctx) =>
                    {
                        TestContext.WriteLine("Sending subscriptions");
                        await Task.WhenAll(
                            session.Subscribe<MyEvent>(),
                            session.Subscribe<MySecondEvent>()
                        );
                        TestContext.WriteLine("Subscriptions sent");
                    });
                })
                .WithEndpoint<NativePubSubSubscriber>(b =>
                {
                    b.When((_, ctx) =>
                    {
                        ctx.SubscribedNative = true;
                        return Task.FromResult(0);
                    });
                })
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(config =>
                    {
                        var settings = config.GetSettings();
                        settings.Set("NServiceBus.AmazonSQS.MessageVisibilityTimeout", testCase.MessageVisibilityTimeout);
                        settings.Set("NServiceBus.AmazonSQS.SubscriptionsCacheTTL", testCase.SubscriptionsCacheTTL);
                        settings.Set("NServiceBus.AmazonSQS.NotFoundTopicsCacheTTL", testCase.NotFoundTopicsCacheTTL);
                    });

                    b.When(c => c.SubscribedMessageDrivenToMyEvent && c.SubscribedMessageDrivenToMySecondEvent && c.SubscribedNative, session =>
                    {
                        return session.SendLocal(new KickOff { NumberOfEvents = testCase.NumberOfEvents });
                    });
                })
                .Done(c => c.NativePubSubSubscriberReceivedMyEventCount == testCase.NumberOfEvents
                    && c.MessageDrivenPubSubSubscriberReceivedMyEventCount == testCase.NumberOfEvents
                    && c.MessageDrivenPubSubSubscriberReceivedMySecondEventCount == testCase.NumberOfEvents)
                .Run(testCase.TestExecutionTimeout);

            Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedMyEventCount);
            Assert.AreEqual(testCase.NumberOfEvents, context.NativePubSubSubscriberReceivedMyEventCount);
            Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedMySecondEventCount);
        }

        public class Context : ScenarioContext
        {
            int nativePubSubSubscriberReceivedMyEventCount;
            internal void IncrementNativePubSubSubscriberReceivedMyEventCount()
            {
                Interlocked.Increment(ref nativePubSubSubscriberReceivedMyEventCount);
            }
            public int NativePubSubSubscriberReceivedMyEventCount => nativePubSubSubscriberReceivedMyEventCount;

            int messageDrivenPubSubSubscriberReceivedMyEventCount;
            internal void IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount()
            {
                Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMyEventCount);
            }
            public int MessageDrivenPubSubSubscriberReceivedMyEventCount => messageDrivenPubSubSubscriberReceivedMyEventCount;

            int messageDrivenPubSubSubscriberReceivedMySecondEventCount;
            internal void IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount()
            {
                Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMySecondEventCount);
            }
            public int MessageDrivenPubSubSubscriberReceivedMySecondEventCount => messageDrivenPubSubSubscriberReceivedMySecondEventCount;

            public bool SubscribedMessageDrivenToMyEvent { get; set; }
            public bool SubscribedMessageDrivenToMySecondEvent { get; set; }
            public bool SubscribedNative { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var subscriptionStorage = new TestingInMemorySubscriptionStorage();
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
#pragma warning disable CS0618
                    c.ConfigureSqsTransport().EnableMessageDrivenPubSubCompatibilityMode();
#pragma warning restore CS0618

                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        TestContext.WriteLine($"Received subscription message {s.MessageType} from {s.SubscriberEndpoint}.");
                        if (!s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(MessageDrivenPubSubSubscriber))))
                        {
                            return;
                        }

                        if (Type.GetType(s.MessageType) == typeof(MyEvent))
                        {
                            context.SubscribedMessageDrivenToMyEvent = true;
                        }

                        if (Type.GetType(s.MessageType) == typeof(MySecondEvent))
                        {
                            context.SubscribedMessageDrivenToMySecondEvent = true;
                        }
                        TestContext.WriteLine($"Subscription message processed.");
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
            }

            public class KickOffMessageHandler : IHandleMessages<KickOff>
            {
                public Task Handle(KickOff message, IMessageHandlerContext context)
                {
                    var tasks = new List<Task>();
                    for (int i = 0; i < message.NumberOfEvents; i++)
                    {
                        tasks.Add(context.Publish(new MyEvent()));
                        tasks.Add(context.Publish(new MySecondEvent()));
                    }
                    return Task.WhenAll(tasks);
                }
            }
        }

        public class NativePubSubSubscriber : EndpointConfigurationBuilder
        {
            public NativePubSubSubscriber()
            {
                EndpointSetup<DefaultServer>(c => { });
            }

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyEventMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementNativePubSubSubscriberReceivedMyEventCount();
                    return Task.FromResult(0);
                }
            }
        }

        public class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
        {
            public MessageDrivenPubSubSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                    {
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher)))),
                        new PublisherTableEntry(typeof(MySecondEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher))))
                    });
                },
                metadata =>
                {
                    metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher));
                    metadata.RegisterPublisherFor<MySecondEvent>(typeof(Publisher));
                });
            }

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyEventMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount();
                    return Task.FromResult(0);
                }
            }

            public class MySecondEventMessageHandler : IHandleMessages<MySecondEvent>
            {
                Context testContext;

                public MySecondEventMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MySecondEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount();
                    return Task.FromResult(0);
                }
            }
        }

        public class KickOff : ICommand
        {
            public int NumberOfEvents { get; set; }
        }

        public class MyEvent : IEvent
        {
        }

        public class MySecondEvent : IEvent
        {
        }
    }
}
