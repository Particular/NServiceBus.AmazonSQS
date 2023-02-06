﻿namespace NServiceBus.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_using_large_message_with_unencrypted_bucket : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_message()
        {
            var payloadToSend = new byte[PayloadSize];

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new MyMessageWithLargePayload
                {
                    Payload = payloadToSend
                }))
                )
                .Done(c => c.ReceivedPayload != null)
                .Run();

            Assert.AreEqual(payloadToSend, context.ReceivedPayload, "The large payload should be handled correctly using the unencrypted S3 bucket");

            var s3Client = ConfigureEndpointSqsTransport.CreateS3Client();

            Assert.DoesNotThrowAsync(async () => await s3Client.GetObjectAsync(ConfigureEndpointSqsTransport.S3BucketName, $"{ConfigureEndpointSqsTransport.S3Prefix}/{context.MessageId}"));
        }

        const int PayloadSize = 150 * 1024;

        public class Context : ScenarioContext
        {
            public byte[] ReceivedPayload { get; set; }
            public string MessageId { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureSqsTransport().S3
                        = new S3Settings(ConfigureEndpointSqsTransport.S3BucketName, ConfigureEndpointSqsTransport.S3Prefix, ConfigureEndpointSqsTransport.CreateS3Client());
                });

            public class MyMessageHandler : IHandleMessages<MyMessageWithLargePayload>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessageWithLargePayload messageWithLargePayload, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageId;
                    testContext.ReceivedPayload = messageWithLargePayload.Payload;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class MyMessageWithLargePayload : ICommand
        {
            public byte[] Payload { get; set; }
        }
    }
}