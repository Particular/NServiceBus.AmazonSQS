﻿namespace NServiceBus.AcceptanceTests.NativePubSub;

using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

public class When_customizing_topic_name_generation : NServiceBusAcceptanceTest
{
    [Test]
    public async Task It_can_subscribe_for_event_published_on_custom_topic()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<CustomizedPublisher>(b => b.When(c => c.Subscribed, (session, ctx) => session.Publish(new MyEvent())))
            .WithEndpoint<CustomizedSubscriber>(b => b.When(async (session, ctx) =>
            {
                await session.Subscribe<MyEvent>();
                ctx.Subscribed = true;
            }))
            .Done(c => c.GotTheEvent)
            .Run();

        Assert.That(context.GotTheEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool GotTheEvent { get; set; }
        public bool Subscribed { get; set; }
    }

    public class CustomizedPublisher : EndpointConfigurationBuilder
    {
        public CustomizedPublisher()
        {
            EndpointSetup<DefaultPublisher>(c =>
            {
                c.ConfigureSqsTransport().TopicNameGenerator = (eventType, prefix) => prefix + "-shared-topic";
            });
        }
    }

    public class CustomizedSubscriber : EndpointConfigurationBuilder
    {
        public CustomizedSubscriber()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.ConfigureSqsTransport().TopicNameGenerator = (eventType, prefix) => prefix + "-shared-topic";
            });
        }

        public class MyHandler : IHandleMessages<MyEvent>
        {
            Context testContext;

            public MyHandler(Context testContext)
            {
                this.testContext = testContext;
            }

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