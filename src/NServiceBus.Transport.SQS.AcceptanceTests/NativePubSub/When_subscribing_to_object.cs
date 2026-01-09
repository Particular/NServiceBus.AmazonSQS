namespace NServiceBus.AcceptanceTests.NativePubSub;

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
            .WithEndpoint<Publisher>(b => b.When(session => session.Publish(new MyEvent())))
            .WithEndpoint<Subscriber>(b => { })
            .Run();

        Assert.That(context.GotTheEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool GotTheEvent { get; set; }
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
                c.Conventions().DefiningEventsAs(t => typeof(object).IsAssignableFrom(t) || typeof(IEvent).IsAssignableFrom(t));

                var transport = c.ConfigureSqsTransport();
                transport.MapEvent<object, MyEvent>();
            });

        public class MyHandler(Context testContext) : IHandleMessages<object>
        {
            public Task Handle(object @event, IMessageHandlerContext context)
            {
                testContext.GotTheEvent = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent;
}