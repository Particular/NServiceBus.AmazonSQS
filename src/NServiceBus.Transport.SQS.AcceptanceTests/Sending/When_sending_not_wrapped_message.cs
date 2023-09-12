namespace NServiceBus.AcceptanceTests.Sending
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_not_wrapped_message : NServiceBusAcceptanceTest
    {
        public static object[] Payload =
        {
            new object[] { new byte[4] },
            new object[] { new byte[500 * 1024] }
        };

        [Test]
        [TestCaseSource(nameof(Payload))]
        public async Task Should_receive_message(byte[] payload)
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session => session.Send(new MyMessageWithPayload() { Payload = payload })))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();

            Assert.That(context.Received, Is.True);
            Assert.AreEqual(payload, context.ReceivedPayload, "The payload should be handled correctly");
        }

        public class Context : ScenarioContext
        {
            public byte[] ReceivedPayload { get; set; }
            public bool Received { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRouting().RouteToEndpoint(typeof(MyMessageWithPayload), typeof(Receiver));
                    builder.ConfigureSqsTransport().DoNotWrapOutgoingMessages = true;
                });

            public class Handler : IHandleMessages<Reply>
            {
                public Handler(Context testContext)
                    => this.testContext = testContext;

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    testContext.Received = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServer>();

            public class MyMessageHandler : IHandleMessages<MyMessageWithPayload>
            {
                public MyMessageHandler(Context testContext)
                   => this.testContext = testContext;

                public Task Handle(MyMessageWithPayload message, IMessageHandlerContext context)
                {
                    testContext.ReceivedPayload = message.Payload;
                    return context.Reply(new Reply());
                }

                readonly Context testContext;
            }

        }

        public class MyMessageWithPayload : ICommand
        {
            public byte[] Payload { get; set; }
        }

        public class Reply : IMessage
        {
        }
    }
}