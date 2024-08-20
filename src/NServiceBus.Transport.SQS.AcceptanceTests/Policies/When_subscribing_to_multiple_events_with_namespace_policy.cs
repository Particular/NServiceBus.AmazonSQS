﻿namespace NServiceBus.AcceptanceTests.Policies
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using A;
    using B;

    public class When_subscribing_to_multiple_events_with_namespace_policy : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_events()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(async session =>
                {
                    await session.Publish(new MyEvent());
                    await session.Publish(new MyOtherEvent());
                }))
                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var policies = c.ConfigureSqsTransport().Policies;
                        policies.TopicNamespaceConditions.Add("NServiceBus.AcceptanceTests.Policies.");
                    });
                })
                .Done(c => c.GotEvents)
                .Run();

            Assert.That(context.GotMyEvent, Is.True);
            Assert.That(context.GotMyOtherEvent, Is.True);
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
    }
}

namespace NServiceBus.AcceptanceTests.Policies.A
{
    public class MyEvent : IEvent
    {
    }
}
namespace NServiceBus.AcceptanceTests.Policies.B
{
    public class MyOtherEvent : IEvent
    {
    }
}