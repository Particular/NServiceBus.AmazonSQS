﻿namespace NServiceBus.AcceptanceTests.Policies;

using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

public class When_subscribing_to_multiple_events_with_wildcard_policy_assumed : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_events()
    {
        // settles policy
        await Scenario.Define<Context>()
            .WithEndpoint<Publisher>()
            .WithEndpoint<Subscriber>(b => b.CustomConfig(c =>
            {
                var policies = c.ConfigureSqsTransport().Policies;
                // the actual policy doesn't matter as long as it is a wildcard matching
                policies.AccountCondition = true;
            }))
            .Done(c => c.EndpointsStarted)
            .Run();

        // actual run with publish
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(async session =>
            {
                await session.Publish(new MyEvent());
                await session.Publish(new MyOtherEvent());
            }))
            .WithEndpoint<Subscriber>(b => b.CustomConfig(c =>
            {
                c.ConfigureSqsTransport().Policies.SetupTopicPoliciesWhenSubscribing = false;
            }))
            .Done(c => c.GotEvents)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.GotMyEvent, Is.True);
            Assert.That(context.GotMyOtherEvent, Is.True);
        });
    }

    public class Context : ScenarioContext
    {
        public bool GotMyEvent { get; set; }
        public bool GotMyOtherEvent { get; set; }

        public bool GotEvents => GotMyEvent && GotMyOtherEvent;
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher()
        {
            EndpointSetup<DefaultPublisher>();
        }
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber()
        {
            EndpointSetup<DefaultServer>();
        }

        public class MyHandler : IHandleMessages<MyEvent>
        {
            Context testContext;

            public MyHandler(Context testContext)
            {
                this.testContext = testContext;
            }

            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                testContext.GotMyEvent = true;
                return Task.CompletedTask;
            }
        }

        public class MyEventOtherHandler : IHandleMessages<MyEvent>
        {
            Context testContext;

            public MyEventOtherHandler(Context testContext)
            {
                this.testContext = testContext;
            }

            public Task Handle(MyEvent message, IMessageHandlerContext context)
            {
                testContext.GotMyOtherEvent = true;
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent
    {
    }

    public class MyOtherEvent : IEvent
    {
    }
}