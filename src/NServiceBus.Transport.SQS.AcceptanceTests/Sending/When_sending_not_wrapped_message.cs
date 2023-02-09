namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_not_wrapped_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session => session.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();

            Assert.That(context.Received, Is.True);
        }

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRouting().RouteToEndpoint(typeof(Message), typeof(Receiver));
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

            public class MyMessageHandler : IHandleMessages<Message>
            {
                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    return context.Reply(new Reply());
                }
            }

        }

        public class Message : ICommand
        {
        }

        public class Reply : IMessage
        {
        }
    }
}