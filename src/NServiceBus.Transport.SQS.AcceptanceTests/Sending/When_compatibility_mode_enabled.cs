﻿namespace NServiceBus.AcceptanceTests
{
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

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
            public Sender() =>
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRouting().RouteToEndpoint(typeof(Message), typeof(Receiver));
                    builder.ConfigureSqsTransport().EnableV1CompatibilityMode = true;
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
                public MyMessageHandler(Context testContext)
                    => this.testContext = testContext;

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.MessageContent = JsonNode.Parse(context.Extensions.Get<Amazon.SQS.Model.Message>().Body);
                    return context.Reply(new Reply());
                }

                readonly Context testContext;
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