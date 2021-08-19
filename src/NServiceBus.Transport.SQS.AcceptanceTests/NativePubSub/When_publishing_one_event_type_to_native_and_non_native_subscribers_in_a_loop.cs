namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_one_event_type_to_native_and_non_native_subscribers_in_a_loop : NServiceBusAcceptanceTest
    {
        [Test]
        [TestCase(300)]
        [TestCase(3000)]
        public void Should_not_rate_exceed(int numberOfEvents)
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Publisher>(b =>
                    {
                        b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, session =>
                        {
                            var tasks = new List<Task>();
                            for (int i = 0; i < numberOfEvents; i++)
                            {
                                tasks.Add(session.Publish(new MyEvent()));
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
                        b.When((session, ctx) => session.Subscribe<MyEvent>());
                    })
                    .Done(c => c.NativePubSubSubscriberReceivedEventsCount == numberOfEvents && c.MessageDrivenPubSubSubscriberReceivedEventsCount == numberOfEvents)
                    .Run();
            });
        }

        public class Context : ScenarioContext
        {
            public int NativePubSubSubscriberReceivedEventsCount { get; set; }
            public int MessageDrivenPubSubSubscriberReceivedEventsCount { get; set; }
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
                    testContext.NativePubSubSubscriberReceivedEventsCount++;
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
                    testContext.MessageDrivenPubSubSubscriberReceivedEventsCount++;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}
