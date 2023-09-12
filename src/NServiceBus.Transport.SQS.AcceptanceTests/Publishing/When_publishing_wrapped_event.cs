namespace NServiceBus.AcceptanceTests.Publishing
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;

    public class When_publishing_wrapped_event : NServiceBusAcceptanceTest
    {
        public static object[] Payload =
        {
            new object[] { new byte[4] },
            new object[] { new byte[500 * 1024] }
        };

        [Test]
        [TestCaseSource(nameof(Payload))]
        public async Task Should_receive_event(byte[] payload)
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(c => c.EndpointsStarted, session => session.Publish(new MyEventWithPayload() { Payload = payload })))
                .WithEndpoint<Subscriber>(b => { })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.True(context.GotTheEvent);
            Assert.AreEqual(payload, context.ReceivedPayload, "The payload should be handled correctly");
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public byte[] ReceivedPayload { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c => { }).IncludeType<TestingInMemorySubscriptionPersistence>();
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c => { });
            }

            public class MyHandler : IHandleMessages<MyEventWithPayload>
            {
                readonly Context testContext;

                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEventWithPayload @event, IMessageHandlerContext context)
                {
                    testContext.GotTheEvent = true;
                    testContext.ReceivedPayload = @event.Payload;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyEventWithPayload : IEvent
        {
            public byte[] Payload { get; set; }
        }
    }
}