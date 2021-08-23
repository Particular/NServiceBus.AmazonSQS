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

    public class When_publishing_one_event_type_to_native_and_non_native_subscribers_in_a_loop_in_the_context_of_incoming_message : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases = new TestCase[]
{
            new TestCase{ NumberOfEvents = 1 },
            new TestCase{ NumberOfEvents = 100 },
            new TestCase{ NumberOfEvents = 200, MessageVisibilityTimeout = 45 },
            new TestCase{ NumberOfEvents = 300, MessageVisibilityTimeout = 60 },
            new TestCase{ NumberOfEvents = 1000, MessageVisibilityTimeout = 120, TestExecutionTimeout = TimeSpan.FromMinutes(4) },
            new TestCase{ NumberOfEvents = 3000, MessageVisibilityTimeout = 120, TestExecutionTimeout = TimeSpan.FromMinutes(4) },
};

        [Test, TestCaseSource(nameof(TestCases))]
        public void Should_not_rate_exceed(TestCase testCase)
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Publisher>(b =>
                    {
                        b.CustomConfig(config =>
                        {
                            var settings = config.GetSettings();
                            settings.Set("NServiceBus.AmazonSQS.MessageVisibilityTimeout", testCase.MessageVisibilityTimeout);
                        });

                        b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, session =>
                        {
                            return session.SendLocal(new KickOff { NumberOfEvents = testCase.NumberOfEvents });
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
                    .WithEndpoint<MessageDrivenPubSubSubscriber>(b =>
                    {
                        b.When((session, ctx) => session.Subscribe<MyEvent>());
                    })
                    .Done(c => c.NativePubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents
                    && c.MessageDrivenPubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents)
                    .Run(testCase.TestExecutionTimeout);
            });
        }

        public class Context : ScenarioContext
        {
            public int NativePubSubSubscriberReceivedEventsCount;
            public int MessageDrivenPubSubSubscriberReceivedEventsCount;
            public bool SubscribedMessageDriven { get; set; }
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
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(MessageDrivenPubSubSubscriber))))
                        {
                            context.SubscribedMessageDriven = true;
                        }
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
                    Interlocked.Increment(ref testContext.NativePubSubSubscriberReceivedEventsCount);
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
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher))))
                    });
                },
                metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));
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
                    Interlocked.Increment(ref testContext.MessageDrivenPubSubSubscriberReceivedEventsCount);
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
    }
}
