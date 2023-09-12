namespace NServiceBus.AcceptanceTests.Sending
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Configuration.AdvancedExtensibility;
    using AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQS.Configure;

    public class When_using_small_message_with_no_bucket_configured : NServiceBusAcceptanceTest
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
            public Endpoint() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                    c.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                });

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
                {
                    testContext.ReceivedPayload = messageWithLargePayload.Payload;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class MyMessage : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}