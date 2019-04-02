namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Configuration.AdvanceExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Sending_small_message_with_no_bucket_configured : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_message()
        {
            var payloadToSend = new byte[10];
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new MyMessage
                    {
                        Payload = payloadToSend
                    }))
                )
                .Done(c => c.ReceivedPayload != null)
                .Run();

            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "Payload should be received");
        }

        public class Context : ScenarioContext
        {
            public byte[] ReceivedPayload { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                    c.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
                {
                    Context.ReceivedPayload = messageWithLargePayload.Payload;

                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}