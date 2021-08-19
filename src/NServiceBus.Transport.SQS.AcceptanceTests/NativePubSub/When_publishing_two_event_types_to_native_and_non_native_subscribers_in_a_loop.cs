namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_two_event_types_to_native_and_non_native_subscribers_in_a_loop : NServiceBusAcceptanceTest
    {
        [Test]
        [TestCase(300)]
        public void Should_not_rate_exceed(int numberOfEvents)
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Publisher>(b =>
                    {
                        b.When(c => c.SubscribedMessageDrivenToMyEvent && c.SubscribedMessageDrivenToMySecondEvent && c.SubscribedNative, session =>
                        {
                            var tasks = new List<Task>();
                            for (int i = 0; i < numberOfEvents; i++)
                            {
                                tasks.Add(session.Publish(new MyEvent()));
                                tasks.Add(session.Publish(new MySecondEvent()));
                            }
                            return Task.WhenAll(tasks);
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
                        b.When(async (session, ctx) =>
                        {
                            await session.Subscribe<MyEvent>();
                            await session.Subscribe<MySecondEvent>();
                        });
                    })
                    .Done(c => c.NativePubSubSubscriberReceivedMyEventCount == numberOfEvents
                        && c.MessageDrivenPubSubSubscriberReceivedMyEventCount == numberOfEvents
                        && c.MessageDrivenPubSubSubscriberReceivedMySecondEventCount == numberOfEvents)
                    .Run();
            });
        }

        public class Context : ScenarioContext
        {
            public int NativePubSubSubscriberReceivedMyEventCount { get; set; }
            public int MessageDrivenPubSubSubscriberReceivedMyEventCount { get; set; }
            public int MessageDrivenPubSubSubscriberReceivedMySecondEventCount { get; set; }
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
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
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
                    testContext.NativePubSubSubscriberReceivedMyEventCount++;
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
                    testContext.MessageDrivenPubSubSubscriberReceivedMyEventCount++;
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
                    testContext.MessageDrivenPubSubSubscriberReceivedMySecondEventCount++;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }

        public class MySecondEvent : IEvent
        {
        }
    }
}
