namespace NServiceBus.AcceptanceTests.Sending
{
    using System;
    using AcceptanceTesting;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQS.Configure;

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
                    c.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, string.Empty);
                    c.GetSettings().Set(SettingsKeys.S3KeyPrefix, string.Empty);
                });
            }
        }

        public class MyMessageWithLargePayload : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}