namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;

    public class When_concurrent_subscribe_with_scaled_subscriber : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_still_deliver()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.Instance1Subscribed && c.Instance2Subscribed, (session, _) =>
                    Task.WhenAll(session.Publish(new MyEvent()), session.Publish(new MyOtherEvent()))))
                .WithEndpoint<Subscriber>(b => b.When(async (session, ctx) =>
                {
                    await Task.WhenAll(session.Subscribe<MyEvent>(), session.Subscribe<MyOtherEvent>());
                    ctx.Instance1Subscribed = true;
                }))
                .WithEndpoint<Subscriber>(b => b.When(async (session, ctx) =>
                {
                    await Task.WhenAll(session.Subscribe<MyEvent>(), session.Subscribe<MyOtherEvent>());
                    ctx.Instance2Subscribed = true;
                }))
                .Done(c => c.GotTheEvent && c.GotTheOtherEvent)
                .Run();

            Assert.True(context.GotTheEvent);
            Assert.True(context.GotTheOtherEvent);
        }

        public class Context : ScenarioContext
        {
            public bool Instance1Subscribed { get; set; }
            public bool Instance2Subscribed { get; set; }
            public bool GotTheEvent { get; set; }
            public bool GotTheOtherEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() => EndpointSetup<DefaultPublisher>();
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.ConfigureSqsTransport();
                    // Due to optimistic concurrency on queue metadata modifications it is required
                    // to make sure the same policy outcome is achieved for all concurrent subscribes
                    // otherwise policies might be partial which can lead to message loss
                    transport.Policies.TopicNamespaceConditions.Add("NServiceBus.AcceptanceTests.NativePubSub");

                    c.DisableFeature<AutoSubscribe>();
                });

            public class MyHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }

            public class MyInterfaceHandler : IHandleMessages<MyOtherEvent>
            {
                Context testContext;

                public MyInterfaceHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyOtherEvent @event, IMessageHandlerContext context)
                {
                    testContext.GotTheOtherEvent = true;
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