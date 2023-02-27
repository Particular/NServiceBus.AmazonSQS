namespace NServiceBus.AcceptanceTests.Sending
{
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;
    using JsonObject = SimpleJson.JsonObject;

    public class When_compatibility_mode_enabled : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session => session.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();

            // ReplyToAddress and TimeToBeReceived need to be propagated to the transport message as properties
            Assert.That(context.MessageContent, Contains.Key("ReplyToAddress"));
            Assert.That(context.MessageContent, Contains.Key("TimeToBeReceived"));
        }

        public class Context : ScenarioContext
        {
            public JsonNode MessageContent { get; set; }
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
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.MessageContent = JsonNode.Parse(context.Extensions.Get<Amazon.SQS.Model.Message>().Body);
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