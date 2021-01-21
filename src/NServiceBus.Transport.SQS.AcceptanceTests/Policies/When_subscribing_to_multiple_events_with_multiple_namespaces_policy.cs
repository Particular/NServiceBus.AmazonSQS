namespace NServiceBus.AcceptanceTests.Policies
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using C;
    using D;

    public class When_subscribing_to_multiple_events_with_multiple_namespaces_policy : NServiceBusAcceptanceTest
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
                        policies.TopicNamespaceConditions.Add("NServiceBus.AcceptanceTests.Policies.C");
                        policies.TopicNamespaceConditions.Add("NServiceBus.AcceptanceTests.Policies.D");
                    });
                })
                .Done(c => c.GotMyOtherEvent)
                .Run();

            Assert.IsTrue(context.GotMyEvent);
            Assert.IsTrue(context.GotMyOtherEvent);
        }

        public class Context : ScenarioContext
        {
            public bool GotMyEvent { get; set; }
            public bool GotMyOtherEvent { get; set; }
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

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyEventHandler(Context testContext)
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

namespace NServiceBus.AcceptanceTests.Policies.C
{
    public class MyEvent : IEvent
    {
    }
}
namespace NServiceBus.AcceptanceTests.Policies.D
{
    public class MyOtherEvent : IEvent
    {
    }
}