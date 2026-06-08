namespace NServiceBus.AcceptanceTests.NativePubSub;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;
using EndpointTemplates;
using Features;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NUnit.Framework;
using Conventions = AcceptanceTesting.Customization.Conventions;

public class When_migrating_publisher_first : NServiceBusAcceptanceTest
{
    static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

    [Test]
    public async Task Should_not_lose_any_events()
    {
        var subscriptionStorage = new TestingInMemorySubscriptionStorage();

        //Before migration begins
        var beforeMigration = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(false), b =>
            {
                b.CustomConfig(c =>
                {
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                });
                b.When(c => c.SubscribedMessageDriven, session => session.Publish(new MyEvent()));
            })
            .WithEndpoint(new Subscriber(false), b =>
            {
                b.CustomConfig(c =>
                {
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                    [
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                    ]);
                });
                b.When(session => session.Subscribe<MyEvent>());
            })
            .Run();

        Assert.That(beforeMigration.GotTheEvent, Is.True);

        //Publisher migrated and in compatibility mode
        var publisherMigrated = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(true), b =>
            {
                b.CustomConfig(c =>
                {
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
#pragma warning disable CS0618 // Type or member is obsolete
                    // When message-driven compatibility mode is obsoleted with an error this test can be removed
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
#pragma warning restore CS0618 // Type or member is obsolete
                });
                b.When(session => session.Publish(new MyEvent()));
            })
            .WithEndpoint(new Subscriber(false), b =>
            {
                b.CustomConfig(c =>
                {
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                    [
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                    ]);
                });
                b.When(session => session.Subscribe<MyEvent>());
            })
            .Run();

        Assert.That(publisherMigrated.GotTheEvent, Is.True);

        //Subscriber migrated and in compatibility mode
        var subscriberMigrated = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(true), b =>
            {
                b.CustomConfig(c =>
                {
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
#pragma warning disable CS0618 // Type or member is obsolete
                    // When message-driven compatibility mode is obsoleted with an error this test can be removed
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
#pragma warning restore CS0618 // Type or member is obsolete
                });
                b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, session => session.Publish(new MyEvent()));
            })
            .WithEndpoint(new Subscriber(true), b =>
            {
                b.CustomConfig(c =>
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    // When message-driven compatibility mode is obsoleted with an error this test can be removed
                    var compatModeSettings = c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
#pragma warning restore CS0618 // Type or member is obsolete

                    // not needed but left here to enforce duplicates
                    compatModeSettings.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
                });
                b.When(async (session, ctx) =>
                {
                    //Subscribes both using native feature and message-driven
                    await session.Subscribe<MyEvent>();
                    ctx.SubscribedNative = true;
                });
            })
            .Run();

        Assert.That(subscriberMigrated.GotTheEvent, Is.True);

        //Compatibility mode disabled in both publisher and subscriber
        var compatModeDisabled = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(true), b => b.When(session => session.Publish(new MyEvent())))
            .WithEndpoint(new Subscriber(true), _ => { })
            .Run();

        Assert.That(compatModeDisabled.GotTheEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool GotTheEvent { get; set; }
        public bool SubscribedMessageDriven { get; set; }
        public bool SubscribedNative { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher(bool supportsNativePubSub) =>
            EndpointSetup(new CustomizedServer(supportsNativePubSub), (c, rd) =>
            {
                c.OnEndpointSubscribed<Context>((s, context) =>
                {
                    if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(Subscriber))))
                    {
                        context.SubscribedMessageDriven = true;
                    }
                });
            }).IncludeType<TestingInMemorySubscriptionPersistence>();
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber(bool supportsNativePubSub) =>
            EndpointSetup(new CustomizedServer(supportsNativePubSub), (c, rd) =>
                {
                    c.DisableFeature<AutoSubscribe>();
                },
                metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));

        public class MyEventMessageHandler(Context testContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                testContext.GotTheEvent = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent;
}
