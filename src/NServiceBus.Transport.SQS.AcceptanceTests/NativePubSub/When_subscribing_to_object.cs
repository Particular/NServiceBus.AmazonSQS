namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_subscribing_to_object : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_still_deliver()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.EndpointsStarted, session => session.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(b => { })
                .Done(c => c.GotTheEvent)
                .Run();

            Assert.True(context.GotTheEvent);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
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
                    c.Conventions().DefiningEventsAs(t => typeof(object).IsAssignableFrom(t) || typeof(IEvent).IsAssignableFrom(t));

                    var transport = c.ConfigureSqsTransport();
                    transport.MapEvent<object, MyEvent>();
                });
            }

            public class MyHandler : IHandleMessages<object>
            {
                Context testContext;

                public MyHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(object @event, IMessageHandlerContext context)
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