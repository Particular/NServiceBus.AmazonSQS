namespace NServiceBus.AcceptanceTests.Receiving;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

public class When_renewing_visibility : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_allow_handler_to_process()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b => b.When(session => session.SendLocal(new MyMessage())))
            .Done(c => c.Handled)
            .Run();

        Assert.That(context.Handled, Is.True, "The message visibility timeout was not properly extended");
    }

    public class Context : ScenarioContext
    {
        public bool Handled { get; set; }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() =>
            EndpointSetup<DefaultServer>(config =>
            {
                var transport = config.ConfigureSqsTransport();
                transport.MessageVisibilityTimeout = TimeSpan.FromSeconds(1);
            });

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), context.CancellationToken);
                testContext.Handled = true;
            }
        }
    }

    public class MyMessage : ICommand;
}