namespace NServiceBus.AcceptanceTests.Sending;

using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;
using Transport.SQS;

public class When_using_small_message_with_no_bucket_configured : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_receive_message()
    {
        var payloadToSend = new byte[TransportConstraints.SqsMaximumMessageSize - 500];
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new MyMessage
            {
                Payload = payloadToSend
            })))
            .Done(c => c.ReceivedPayload != null)
            .Run();

        Assert.That(context.ReceivedPayload, Is.EqualTo(payloadToSend), "Payload should be received");
    }

    public class Context : ScenarioContext
    {
        public byte[] ReceivedPayload { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>();

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
            {
                testContext.ReceivedPayload = messageWithLargePayload.Payload;

                return Task.CompletedTask;
            }
        }
    }

    public class MyMessage : ICommand
    {
        public byte[] Payload { get; set; }
    }
}