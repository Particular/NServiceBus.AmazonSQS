namespace NServiceBus.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class Sending_oversized_message_without_bucket_configured : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_fail()
        {
            var exception = Assert.ThrowsAsync<Exception>(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new MyMessageWithLargePayload
                    {
                        Payload = new byte[PayloadSize]
                    }))
                    )
                    .Done(c => false)
                    .Run();
            });

            Assert.AreEqual(exception.Message, "Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
        }

        const int PayloadSize = 150 * 1024;

        public class Context : ScenarioContext
        {
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureSqsTransport().S3 = null; //Disable S3
                });
            }
        }

        public class MyMessageWithLargePayload : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}