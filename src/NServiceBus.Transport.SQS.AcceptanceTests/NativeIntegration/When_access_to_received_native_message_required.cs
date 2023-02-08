namespace NServiceBus.AcceptanceTests.NativeIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.Pipeline;
    using NUnit.Framework;

    public class When_access_to_received_native_message_required : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_be_available_from_the_pipeline()
        {
            var scenario = await Scenario.Define<Context>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new Message())))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.True(scenario.HandlerHasAccessToNativeSqsMessage, "The handler should have access to the native message");
            Assert.True(scenario.BehaviorHasAccessToNativeSqsMessage, "The behavior should have access to the native message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() =>
                EndpointSetup<DefaultServer>(c =>
                    c.Pipeline.Register(typeof(MyCustomBehavior), "Behavior that needs access to native message"));

            class MyCustomBehavior : Behavior<IIncomingPhysicalMessageContext>
            {
                public MyCustomBehavior(Context testContext) => this.testContext = testContext;

                public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
                {
                    testContext.BehaviorHasAccessToNativeSqsMessage = context.Extensions.TryGet<Amazon.SQS.Model.Message>(out _);
                    return next();
                }

                readonly Context testContext;
            }

            class MyHandler : IHandleMessages<Message>
            {
                public MyHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.HandlerHasAccessToNativeSqsMessage = context.Extensions.TryGet<Amazon.SQS.Model.Message>(out _);
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class Message : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
            public bool BehaviorHasAccessToNativeSqsMessage { get; set; }
            public bool HandlerHasAccessToNativeSqsMessage { get; set; }
        }
    }
}