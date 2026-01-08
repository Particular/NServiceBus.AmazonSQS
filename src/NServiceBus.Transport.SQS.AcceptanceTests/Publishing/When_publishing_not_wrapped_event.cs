namespace NServiceBus.AcceptanceTests.Publishing;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

public class When_publishing_not_wrapped_event : NServiceBusAcceptanceTest
{
    public static object[] Payload = { new object[] { new byte[4] }, new object[] { new byte[500 * 1024] } };

    [Test]
    [TestCaseSource(nameof(Payload))]
    public async Task Should_receive_event(byte[] payload)
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b => b.When(session => session.Publish(new MyEventWithPayload() { Payload = payload })))
            .WithEndpoint<Subscriber>()
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.ReceivedPayload, Is.EqualTo(payload), "The payload should be handled correctly");
        });
    }

    public class Context : ScenarioContext
    {
        public byte[] ReceivedPayload { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultPublisher>(c => c.ConfigureSqsTransport().DoNotWrapOutgoingMessages = true).IncludeType<TestingInMemorySubscriptionPersistence>();
    }

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber() => EndpointSetup<DefaultServer>(_ => { });

        public class MyHandler(Context testContext) : IHandleMessages<MyEventWithPayload>
        {
            public Task Handle(MyEventWithPayload @event, IMessageHandlerContext context)
            {
                testContext.ReceivedPayload = @event.Payload;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEventWithPayload : IEvent
    {
        public byte[] Payload { get; set; }
    }
}