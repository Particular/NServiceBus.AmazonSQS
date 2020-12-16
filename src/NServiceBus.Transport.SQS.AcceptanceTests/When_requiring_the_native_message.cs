namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    class When_requiring_the_native_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_have_access_to_the_native_message_from_messagehandler()
        {
            var scenario = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(scenario.HandlerHasAccessToNativeSqsMessage, "The handler should have access to the native message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<SqsTransport>());
            }

            class MyEventHandler : IHandleMessages<Message>
            {
                private Context testContext;

                public MyEventHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.HandlerHasAccessToNativeSqsMessage = context.Extensions.TryGet<Amazon.SQS.Model.Message>(out _);
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class Message : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }

            public bool HandlerHasAccessToNativeSqsMessage { get; set; }
        }
    }
}