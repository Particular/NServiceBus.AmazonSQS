﻿namespace NServiceBus.AcceptanceTests.NativePubSub;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Configuration.AdvancedExtensibility;
using EndpointTemplates;
using Features;
using NUnit.Framework;

public class When_subscriber_runs_in_compat_mode : NServiceBusAcceptanceTest
{
    static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(LegacyPublisher));

    [Test]
    public async Task It_can_subscribe_for_event_published_by_legacy_publisher()
    {
        var publisherMigrated = await Scenario.Define<Context>()
            .WithEndpoint<LegacyPublisher>(b => b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent())))
            .WithEndpoint<MigratedSubscriber>(b => b.When((session, ctx) => session.Subscribe<MyEvent>()))
            .Done(c => c.GotTheEvent)
            .Run(TimeSpan.FromSeconds(30));

        Assert.That(publisherMigrated.GotTheEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool GotTheEvent { get; set; }
        public bool SubscribedMessageDriven { get; set; }
    }

    public class LegacyPublisher : EndpointConfigurationBuilder
    {
        public LegacyPublisher()
        {
            EndpointSetup(new CustomizedServer(false), (c, rd) =>
            {
                c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                c.OnEndpointSubscribed<Context>((s, context) =>
                {
                    if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(MigratedSubscriber))))
                    {
                        context.SubscribedMessageDriven = true;
                    }
                });
            }).IncludeType<TestingInMemorySubscriptionPersistence>();
        }
    }

    public class MigratedSubscriber : EndpointConfigurationBuilder
    {
        public MigratedSubscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                var compatMode = c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                compatMode.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
                c.DisableFeature<AutoSubscribe>();
            });
        }

        public class MyHandler : IHandleMessages<MyEvent>
        {
            readonly Context testContext;

            public MyHandler(Context context) => testContext = context;

            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                testContext.GotTheEvent = true;
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent
    {
    }
}