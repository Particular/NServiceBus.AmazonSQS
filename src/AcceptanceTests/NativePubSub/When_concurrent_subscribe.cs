namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;

    public class When_concurrent_subscribe : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_still_deliver()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.Subscribed, (session, ctx) => Task.WhenAll(session.Publish(new MyEvent()), session.Publish(new MyOtherEvent()))))
                .WithEndpoint<Subscriber>(b => b.When(async (session, ctx) =>
                {
                    await Task.WhenAll(session.Subscribe<MyEvent>(), session.Subscribe<MyOtherEvent>());
                    ctx.Subscribed = true;
                }))
                .Done(c => c.GotTheEvent && c.GotTheOtherEvent)
                .Run();

            Assert.True(context.GotTheEvent);
            Assert.True(context.GotTheOtherEvent);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public bool Subscribed { get; set; }
            public bool GotTheOtherEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c => { });
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.DisableFeature<AutoSubscribe>();
                });
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    Context.GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }

            public class MyInterfaceEventHandler : IHandleMessages<MyOtherEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyOtherEvent @event, IMessageHandlerContext context)
                {
                    Context.GotTheOtherEvent = true;
                    return Task.FromResult(0);
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
}