namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;

    public class When_publisher_runs_in_compat_mode : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(MigratedPublisher));

        [Test]
        public async Task Legacy_subscriber_can_subscribe()
        {
            var publisherMigrated = await Scenario.Define<Context>()
                .WithEndpoint<MigratedPublisher>(b => b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(b => b.When((session, ctx) => session.Subscribe<MyEvent>()))
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.True(publisherMigrated.GotTheEvent);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public bool SubscribedMessageDriven { get; set; }
        }

        public class MigratedPublisher : EndpointConfigurationBuilder
        {
            public MigratedPublisher()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(Subscriber))))
                        {
                            context.SubscribedMessageDriven = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup(new CustomizedServer(false), (c, rd) =>
                {
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                    {
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                    });
                    c.DisableFeature<AutoSubscribe>();
                });
            }

            public class MyHandler : IHandleMessages<MyEvent>
            {
                readonly Context testContext;

                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}