namespace NServiceBus.AcceptanceTests.Sending
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Sending_in_compatibility_mode : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_message()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session => session.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(Message), typeof(Receiver));
                    builder.ConfigureSqsTransport().EnableV1CompatibilityMode();
                });
            }

            public class Handler : IHandleMessages<Reply>
            {
                public Context Context { get; set; }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    Context.Received = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

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